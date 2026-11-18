using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Sales
{
    public sealed class LeadInput
    {
        public Guid? CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? ApartmentHouseId { get; set; }
        public int? Bedrooms { get; set; }
        public double? Budget { get; set; }
        public string? Currency { get; set; }
        public string? Location { get; set; }
        public string Source { get; set; } = "Website";
        public string? Campaign { get; set; }
        public string Temperature { get; set; } = "Warm";
        public string? AgentUserId { get; set; }
        public DateTime? NextFollowUpOn { get; set; }
    }

    /// <summary>
    /// Leads (CRM): created by staff or automatically from customer actions (reservation, site visit, chat, video call),
    /// moved through the pipeline, assigned to agents, with calls, meetings, notes and follow-ups; converted by a sale.
    /// </summary>
    public sealed class LeadService
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly AgentService _agents;

        public LeadService(ApplicationDbContext db, AuditService audit, AgentService agents)
        {
            _db = db;
            _audit = audit;
            _agents = agents;
        }

        public static int Rank(string stage) => Array.IndexOf(LeadStages.All, stage);

        // ---------- Staff ----------

        public async Task<AccessResult> CreateAsync(UserAccess actor, LeadInput input, string? actorName)
        {
            if (!actor.Has(Perm.LeadsManage)) return AccessResult.Fail("You don't have permission to create leads.");
            var companyId = actor.IsPlatform ? input.CompanyId : actor.CompanyId;
            if (companyId is not Guid company || !await _db.HousingCompanies.AnyAsync(c => c.CompanyId == company)) return AccessResult.Fail("Choose the company.");
            var error = await ValidateAsync(actor, company, input, isNew: true);
            if (error is not null) return AccessResult.Fail(error);

            var lead = new Lead { Id = Guid.NewGuid(), Reference = AgentService.NewReference("L"), CompanyId = company, CreatedOn = DateTime.UtcNow };
            Apply(lead, input);
            lead.AgentUserId ??= await _agents.PickAgentAsync(lead.ProjectId);
            _db.Leads.Add(lead);
            Activity(lead, "Created", $"Lead created ({LeadSources.Label(lead.Source)}).", actor.UserId, actorName);
            _audit.Add(AuditCategories.Sales, "LeadCreated", $"Lead {lead.Reference} ({lead.Name}) created.", company, nameof(Lead), lead.Id.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"Lead {lead.Reference} created.", lead.Id.ToString());
        }

        public async Task<AccessResult> UpdateAsync(UserAccess actor, SalesScope scope, Guid leadId, LeadInput input, string? actorName)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead is null || !scope.CanSee(lead.CompanyId, lead.ProjectId, lead.AgentUserId) || !actor.Has(Perm.LeadsManage)) return AccessResult.Fail("That lead isn't yours to edit.");
            input.AgentUserId = lead.AgentUserId; // assignment has its own action
            var error = await ValidateAsync(actor, lead.CompanyId, input, isNew: false);
            if (error is not null) return AccessResult.Fail(error);
            Apply(lead, input);
            Activity(lead, "Note", "Details updated.", actor.UserId, actorName);
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Lead saved.");
        }

        public async Task<AccessResult> ChangeStageAsync(UserAccess actor, SalesScope scope, Guid leadId, string stage, string? lostReason, string? actorName)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead is null || !scope.CanSee(lead.CompanyId, lead.ProjectId, lead.AgentUserId) || !actor.Has(Perm.LeadsManage)) return AccessResult.Fail("That lead isn't yours to change.");
            if (!LeadStages.All.Contains(stage)) return AccessResult.Fail("Unknown stage.");
            lostReason = string.IsNullOrWhiteSpace(lostReason) ? null : lostReason.Trim();
            if (stage == LeadStages.Lost && lostReason is null) return AccessResult.Fail("Say why the lead was lost.");
            var from = lead.Stage;
            lead.Stage = stage;
            lead.LostReason = stage == LeadStages.Lost ? lostReason : null;
            lead.ClosedOn = stage is LeadStages.Converted or LeadStages.Lost ? DateTime.UtcNow : null;
            if (lead.ClosedOn is not null) lead.NextFollowUpOn = null;
            Activity(lead, "Stage", $"{LeadStages.Label(from)} → {LeadStages.Label(stage)}{(lostReason is null ? "" : $": {lostReason}")}", actor.UserId, actorName);
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"Moved to {LeadStages.Label(stage)}.");
        }

        public async Task<AccessResult> AssignAsync(UserAccess actor, SalesScope scope, Guid leadId, string? agentUserId, string? actorName)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead is null || !scope.CanSee(lead.CompanyId, lead.ProjectId, lead.AgentUserId) || !actor.Has(Perm.LeadsAssign)) return AccessResult.Fail("You can't assign that lead.");
            string? name = null;
            if (agentUserId is not null)
            {
                var agent = await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == agentUserId && u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == lead.CompanyId);
                if (agent is null) return AccessResult.Fail("Choose one of the company's staff.");
                name = AccessManagementService.DisplayName(agent);
            }
            lead.AgentUserId = agentUserId;
            Activity(lead, "Assigned", name is null ? "Unassigned." : $"Assigned to {name}.", actor.UserId, actorName);
            await _db.SaveChangesAsync();
            return AccessResult.Ok(name is null ? "Lead unassigned." : $"Lead assigned to {name}.");
        }

        public async Task<AccessResult> AddActivityAsync(UserAccess actor, SalesScope scope, Guid leadId, string type, string? body, DateTime? nextFollowUpOn, string? actorName)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead is null || !scope.CanSee(lead.CompanyId, lead.ProjectId, lead.AgentUserId) || !actor.Has(Perm.LeadsManage)) return AccessResult.Fail("That lead isn't yours to update.");
            if (!LeadActivityTypes.Manual.Contains(type)) return AccessResult.Fail("Choose call, meeting, email or note.");
            body = (body ?? string.Empty).Trim();
            if (body.Length is 0 or > 2000) return AccessResult.Fail("Write what happened (up to 2000 characters).");
            if (nextFollowUpOn is DateTime f && f < DateTime.Now.AddMinutes(-5)) return AccessResult.Fail("The next follow-up must be in the future.");
            Activity(lead, type, body, actor.UserId, actorName);
            if (type != "Note")
            {
                lead.LastContactOn = DateTime.UtcNow;
                if (lead.Stage == LeadStages.New) lead.Stage = LeadStages.Contacted;
            }
            if (nextFollowUpOn is not null) lead.NextFollowUpOn = nextFollowUpOn;
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Recorded.");
        }

        // ---------- Automatic (staged; saved with the caller's change) ----------

        /// <summary>
        /// A customer did something with a company (reserved, asked for a visit, chatted, called): their open lead with that
        /// company gets an activity and moves forward to at least <paramref name="stageAtLeast"/>; without one, a lead is
        /// created. Nothing is saved here.
        /// </summary>
        public async Task<Lead> TouchAsync(Guid companyId, Guid? projectId, Guid? apartmentId, Customer customer, string source, string activityType, string text, string? stageAtLeast = null)
        {
            var lead = _db.Leads.Local.FirstOrDefault(l => l.CompanyId == companyId && l.CustomerId == customer.Id && l.ClosedOn == null)
                       ?? await _db.Leads.Where(l => l.CompanyId == companyId && l.CustomerId == customer.Id && l.ClosedOn == null).OrderByDescending(l => l.CreatedOn).FirstOrDefaultAsync();
            if (lead is null)
            {
                lead = new Lead
                {
                    Id = Guid.NewGuid(), Reference = AgentService.NewReference("L"), CompanyId = companyId, ProjectId = projectId, ApartmentHouseId = apartmentId,
                    CustomerId = customer.Id, Name = $"{customer.FirstName} {customer.LastName}".Trim(), Phone = customer.PhoneNumber, Email = customer.Email,
                    Source = source, Temperature = "Warm", CreatedOn = DateTime.UtcNow,
                };
                lead.AgentUserId = await _agents.PickAgentAsync(projectId);
                _db.Leads.Add(lead);
                Activity(lead, "Created", $"Lead created automatically ({LeadSources.Label(source)}).", null, "System");
            }
            lead.ProjectId ??= projectId;
            lead.ApartmentHouseId ??= apartmentId;
            Activity(lead, activityType, text, null, "System");
            if (stageAtLeast is not null && Rank(stageAtLeast) > Rank(lead.Stage) && lead.Stage is not (LeadStages.Converted or LeadStages.Lost)) lead.Stage = stageAtLeast;
            return lead;
        }

        /// <summary>A sale was recorded: the customer's open lead with the company (or a new one) is converted. Staged.</summary>
        public async Task ConvertOnSaleAsync(Guid companyId, Guid? projectId, Guid? apartmentId, Guid customerId, Guid transactionId, string text)
        {
            var customer = await _db.UserProfiles.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer is null) return;
            var lead = await TouchAsync(companyId, projectId, apartmentId, customer, "Reservation", "Sale", text);
            lead.Stage = LeadStages.Converted;
            lead.ClosedOn = DateTime.UtcNow;
            lead.NextFollowUpOn = null;
            lead.SaleTransactionId = transactionId;
        }

        private void Activity(Lead lead, string type, string body, string? userId, string? name) =>
            _db.LeadActivities.Add(new LeadActivity { LeadId = lead.Id, Lead = lead, Type = type, Body = body.Length > 2000 ? body[..2000] : body, OccurredOn = DateTime.UtcNow, ByUserId = userId, ByName = name });

        private static void Apply(Lead lead, LeadInput input)
        {
            lead.Name = input.Name.Trim();
            lead.Phone = Clean(input.Phone);
            lead.Email = Clean(input.Email);
            lead.ProjectId = input.ProjectId;
            lead.ApartmentHouseId = input.ApartmentHouseId;
            lead.Bedrooms = input.Bedrooms;
            lead.Budget = input.Budget;
            lead.Currency = Clean(input.Currency) ?? "ETB";
            lead.Location = Clean(input.Location);
            lead.Source = input.Source;
            lead.Campaign = Clean(input.Campaign);
            lead.Temperature = input.Temperature;
            lead.AgentUserId = input.AgentUserId;
            lead.NextFollowUpOn = input.NextFollowUpOn;
        }

        private async Task<string?> ValidateAsync(UserAccess actor, Guid companyId, LeadInput input, bool isNew)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 150) return "Enter the person's name.";
            if (string.IsNullOrWhiteSpace(input.Phone) && string.IsNullOrWhiteSpace(input.Email)) return "Enter a phone number or an email address.";
            if (!LeadSources.All.Contains(input.Source)) return "Choose where the lead came from.";
            if (!LeadTemperatures.All.Contains(input.Temperature)) return "Choose hot, warm or cold.";
            if (input.Budget is < 0) return "The budget can't be negative.";
            if (input.Bedrooms is < 0 or > 20) return "Check the number of bedrooms.";
            if (input.ProjectId is Guid p && !await _db.Projects.AnyAsync(x => x.ProjectId == p && x.HousingCompanyId == companyId)) return "That project isn't the company's.";
            if (input.ApartmentHouseId is Guid a && !await _db.ApartmentHouses.AnyAsync(x => x.Id == a && x.Building!.Project!.HousingCompanyId == companyId)) return "That home isn't the company's.";
            if (isNew && input.AgentUserId is string agent)
            {
                if (agent != actor.UserId && !actor.Has(Perm.LeadsAssign)) return "You can only assign the lead to yourself.";
                if (!await _db.AppUsers.AnyAsync(u => u.Id == agent && u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == companyId)) return "Choose one of the company's staff.";
            }
            return null;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
