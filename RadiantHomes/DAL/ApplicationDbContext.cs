using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using System.Reflection.Emit;

namespace RadiantHomes.DAL
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<HousePerFloor> HousePerFloors { get; set; }
        public DbSet<Subscriber> Subscriptions { get; set; }
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public DbSet<UILocalization> Translations { get; set; }
        public DbSet<Customer> UserProfiles { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RentInfo> HouseRentInfos { get; set; }
        public DbSet<House> Houses { get; set; }
        public DbSet<HouseCategory> HouseCategories { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<HomeOwnerRanking> HomeOwnerRankings { get; set; }
        public DbSet<HouseRentPayment> HouseRentPayments { get; set; }
        public DbSet<HouseRentTransaction> HouseRentTransactions { get; set; }
        public DbSet<HouseSalePayment> HouseSalePayments { get; set; }
        public DbSet<HouseSaleTransaction> HouseSaleTransactions { get; set; }
        public DbSet<ApartmentHouseSaleTransaction> ApartmentHouseSaleTransactions { get; set; }
        public DbSet<PartyAddress> Addresses { get; set; }
        public DbSet<IssueOrCompliant> IssuesOrCompliants { get; set; }
        public DbSet<CommisionRate> CommisionRates { get; set; }
        public DbSet<HouseReview> HouseReviews { get; set; }
        public DbSet<HousingCompany> HousingCompanies { get; set; }
        public DbSet<CompanyFinancialDetail> HousingCompanyFinancialDetails { get; set; }
        public DbSet<HouseProject> Projects { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<ApartmentHouse> ApartmentHouses { get; set; }
        public DbSet<ApartmentHouseReview> ApartmentHouseReviews { get; set; }
        public DbSet<CustomerTestimonial> CustomerTestimonials { get; set; }
        public DbSet<HouseSpecs> HouseSpecifications { get; set; }
        public DbSet<ApartmentPhoto> ApartmentPhotos { get; set; }
        public DbSet<ReferralCode> ReferralCodes { get; set; }
        public DbSet<Referral> Referrals { get; set; }

        // Access control (Models/Security/AccessControl.cs)
        public DbSet<Models.Security.Permission> Permissions { get; set; }
        public DbSet<Models.Security.AccessRole> AccessRoles { get; set; }
        public DbSet<Models.Security.AccessRolePermission> AccessRolePermissions { get; set; }
        public DbSet<Models.Security.UserAccessRole> UserAccessRoles { get; set; }
        public DbSet<Models.Security.AuditLog> AuditLogs { get; set; }
        public DbSet<Models.Security.CompanyStatusChange> CompanyStatusChanges { get; set; }

        // Foundation 2: KYC, reservation history, upload batches, platform settings
        public DbSet<Models.Kyc.KycApplication> KycApplications { get; set; }
        public DbSet<Models.Kyc.KycDocument> KycDocuments { get; set; }
        public DbSet<Models.Kyc.KycStatusChange> KycStatusChanges { get; set; }
        public DbSet<ReservationStatusChange> ReservationStatusChanges { get; set; }
        public DbSet<UploadBatch> UploadBatches { get; set; }
        public DbSet<UploadBatchRow> UploadBatchRows { get; set; }
        public DbSet<Models.Shared.PlatformSetting> PlatformSettings { get; set; }

        // Foundation 3: agents, chat, site visits, leads, call log
        public DbSet<Models.Sales.ProjectAgentAssignment> ProjectAgentAssignments { get; set; }
        public DbSet<Models.Sales.Conversation> Conversations { get; set; }
        public DbSet<Models.Sales.ChatMessage> ChatMessages { get; set; }
        public DbSet<Models.Sales.SiteVisit> SiteVisits { get; set; }
        public DbSet<Models.Sales.SiteVisitStatusChange> SiteVisitStatusChanges { get; set; }
        public DbSet<Models.Sales.Lead> Leads { get; set; }
        public DbSet<Models.Sales.LeadActivity> LeadActivities { get; set; }
        public DbSet<Models.Sales.CallLog> CallLogs { get; set; }

        // Guid keys have no database default (migration 20251227201651_GUIDCleanUpForRealEstate removed the
        // NEWID() defaults), so EF generates them on the client. Rows added with an explicit Guid.Empty key
        // (some pages build entities that way) get a fresh Guid here as well.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AssignMissingGuidKeys();
            StampAuditFields();
            GuardCompanyStatusAsync(sync: true).GetAwaiter().GetResult();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AssignMissingGuidKeys();
            StampAuditFields();
            await GuardCompanyStatusAsync(sync: false, cancellationToken);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>Set only by Services/Security/CompanyLifecycleService while it applies an approval decision.</summary>
        internal bool CompanyStatusChangeAllowed { get; set; }

        // A company's approval status changes only through the lifecycle service (with history, audit and notification).
        // Any other save that changes it - a form that binds the whole company, an attached entity - keeps the stored
        // value; a new company always starts as Pending, with a "registered" history entry.
        private async Task GuardCompanyStatusAsync(bool sync, CancellationToken cancellationToken = default)
        {
            if (CompanyStatusChangeAllowed) return;
            foreach (var entry in ChangeTracker.Entries<HousingCompany>().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    // Its built-in Company Super Admin role exists from the start, so the first administrator can be named
                    // (and see the approval status) while the company waits for approval.
                    var companyId = entry.Entity.CompanyId;
                    if (!AccessRoles.Local.Any(r => r.CompanyId == companyId && r.IsSystem))
                    {
                        Services.Security.AccessControlSeeder.NewCompanySuperAdmin(this, companyId);
                    }
                    entry.Entity.Status = Models.Security.CompanyStatuses.Pending;
                    CompanyStatusChanges.Add(new Models.Security.CompanyStatusChange
                    {
                        CompanyId = entry.Entity.CompanyId,
                        ToStatus = Models.Security.CompanyStatuses.Pending,
                        Reason = "Company registered; waiting for approval.",
                        ChangedByName = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "System",
                        ChangedOn = DateTime.UtcNow,
                    });
                }
                else if (entry.State == EntityState.Modified && entry.Property(c => c.Status).IsModified)
                {
                    // Attached entities carry the posted value as their "original" too, so compare with the database.
                    var stored = sync ? entry.GetDatabaseValues() : await entry.GetDatabaseValuesAsync(cancellationToken);
                    var storedStatus = stored?.GetValue<string>(nameof(HousingCompany.Status));
                    if (storedStatus is not null && storedStatus != entry.Entity.Status)
                    {
                        entry.Entity.Status = storedStatus;
                    }
                    entry.Property(c => c.Status).IsModified = false;
                }
            }
        }

        // Created/Updated by/on are maintained here for every BaseEntity, so pages don't have to
        // (and edit forms can't overwrite them): new rows get CreatedOn/CreatedBy, edited rows get
        // UpdatedOn/UpdatedBy, and CreatedOn/CreatedBy are never changed by an update.
        private void StampAuditFields()
        {
            var now = DateTime.Now;
            var userName = _httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true
                ? _httpContextAccessor.HttpContext.User.Identity!.Name
                : null;

            foreach (var entry in ChangeTracker.Entries<Models.Shared.BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.CreatedOn == default) entry.Entity.CreatedOn = now;
                    if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy)) entry.Entity.CreatedBy = userName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(e => e.CreatedOn).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    entry.Entity.UpdatedOn = now;
                    entry.Entity.UpdatedBy = userName ?? entry.Entity.UpdatedBy;
                }
            }
        }

        private void AssignMissingGuidKeys()
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
            {
                var key = entry.Metadata.FindPrimaryKey();
                if (key is null) continue;

                foreach (var keyProperty in key.Properties.Where(p => p.ClrType == typeof(Guid)))
                {
                    var property = entry.Property(keyProperty.Name);
                    if (property.IsTemporary || property.CurrentValue is null || (Guid)property.CurrentValue == Guid.Empty)
                    {
                        property.CurrentValue = Guid.NewGuid();
                        property.IsTemporary = false;
                    }
                }
            }
        }


        private static void ConfigureFoundation3(ModelBuilder builder)
        {
            // Links to existing tables are Restrict: addresses and companies already cascade into projects, profiles and
            // accounts, and SQL Server allows one cascade path. Only a record's own children cascade.
            builder.Entity<Models.Sales.ProjectAgentAssignment>(e =>
            {
                e.ToTable("ProjectAgentAssignments");
                e.HasIndex(a => new { a.ProjectId, a.UserId }).IsUnique().HasFilter("[EndedOn] IS NULL"); // one active assignment
                e.HasIndex(a => new { a.UserId, a.EndedOn });
                e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Models.Sales.Conversation>(e =>
            {
                e.ToTable("Conversations");
                e.HasIndex(c => new { c.CompanyId, c.Status, c.LastMessageOn });
                e.HasIndex(c => c.CustomerId);
                e.HasIndex(c => c.AgentUserId);
                e.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.ApartmentHouse).WithMany().HasForeignKey(c => c.ApartmentHouseId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Models.Sales.ChatMessage>(e =>
            {
                e.ToTable("ChatMessages");
                e.HasIndex(m => new { m.ConversationId, m.ClientMessageId }).IsUnique(); // a message sent twice is stored once
                e.HasIndex(m => new { m.ConversationId, m.Id });
                e.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Models.Sales.SiteVisit>(e =>
            {
                e.ToTable("SiteVisits");
                e.HasIndex(v => v.Reference).IsUnique();
                e.HasIndex(v => new { v.CompanyId, v.Status, v.PreferredDate });
                e.HasIndex(v => new { v.AgentUserId, v.ScheduledStart });
                e.HasIndex(v => v.CustomerId);
                e.HasOne(v => v.Company).WithMany().HasForeignKey(v => v.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(v => v.Project).WithMany().HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(v => v.ApartmentHouse).WithMany().HasForeignKey(v => v.ApartmentHouseId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(v => v.Customer).WithMany().HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Models.Sales.SiteVisitStatusChange>(e =>
            {
                e.ToTable("SiteVisitStatusChanges");
                e.HasOne(h => h.SiteVisit).WithMany(v => v.History).HasForeignKey(h => h.SiteVisitId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Models.Sales.Lead>(e =>
            {
                e.ToTable("Leads");
                e.HasIndex(l => l.Reference).IsUnique();
                e.HasIndex(l => new { l.CompanyId, l.Stage });
                e.HasIndex(l => l.AgentUserId);
                e.HasIndex(l => l.CustomerId);
                e.HasIndex(l => l.NextFollowUpOn);
                e.HasOne(l => l.Company).WithMany().HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(l => l.Project).WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(l => l.ApartmentHouse).WithMany().HasForeignKey(l => l.ApartmentHouseId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(l => l.Customer).WithMany().HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Models.Sales.LeadActivity>(e =>
            {
                e.ToTable("LeadActivities");
                e.HasIndex(a => new { a.LeadId, a.OccurredOn });
                e.HasOne(a => a.Lead).WithMany(l => l.Activities).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Models.Sales.CallLog>(e =>
            {
                e.ToTable("CallLogs");
                e.HasIndex(c => c.Room).IsUnique();
                e.HasIndex(c => new { c.CompanyId, c.RequestedOn });
                e.HasIndex(c => new { c.Status, c.RequestedOn });
                e.Ignore(c => c.DurationSeconds);
            });
        }

        private static void ConfigureFoundation2(ModelBuilder builder)
        {
            // KYC
            builder.Entity<Customer>().Property(c => c.KycStatus).HasDefaultValue(Models.Kyc.KycStatuses.NotSubmitted);
            builder.Entity<Models.Kyc.KycApplication>(e =>
            {
                e.ToTable("KycApplications");
                e.HasIndex(a => new { a.CustomerId, a.SubmittedOn });
                e.HasIndex(a => new { a.Status, a.SubmittedOn });
                // Profiles are deleted with their company; applications go with the profile.
                e.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Models.Kyc.KycDocument>(e =>
            {
                e.ToTable("KycDocuments");
                e.HasOne(d => d.Application).WithMany(a => a.Documents).HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Models.Kyc.KycStatusChange>(e =>
            {
                e.ToTable("KycStatusChanges");
                e.HasOne(h => h.Application).WithMany(a => a.History).HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            });

            // Reservations: a reference, at most ONE active (pending/approved) reservation and ONE sale per apartment,
            // enforced by the database so two simultaneous requests can't both succeed.
            builder.Entity<ApartmentHouseSaleTransaction>(e =>
            {
                e.HasIndex(t => t.ApartmentHouseId); // every reservation of an apartment (the filtered ones below cover only some rows)
                e.HasIndex(t => t.Reference).IsUnique().HasFilter("[Reference] <> N''");
                e.HasIndex(t => t.ApartmentHouseId, "IX_ApartmentHouseSaleTransactions_OneActivePerApartment")
                    .IsUnique().HasFilter("[Status] IN (0, 1)");
                e.HasIndex(t => t.ApartmentHouseId, "IX_ApartmentHouseSaleTransactions_OneSalePerApartment")
                    .IsUnique().HasFilter("[Status] = 2");
                e.HasIndex(t => new { t.Status, t.ExpiresAt });
                e.HasIndex(t => t.CompanyId);
            });
            builder.Entity<ReservationStatusChange>(e =>
            {
                e.ToTable("ReservationStatusChanges");
                e.HasIndex(h => new { h.TransactionId, h.ChangedOn });
                e.HasOne(h => h.Transaction).WithMany(t => t.History).HasForeignKey(h => h.TransactionId).OnDelete(DeleteBehavior.Cascade);
            });

            // Upload batches
            builder.Entity<UploadBatch>(e =>
            {
                e.ToTable("UploadBatches");
                e.HasIndex(b => b.Reference).IsUnique();
                e.HasIndex(b => new { b.Status, b.UploadedOn });
                e.HasIndex(b => b.CompanyId);
                e.HasOne(b => b.Company).WithMany().HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(b => b.Building).WithMany().HasForeignKey(b => b.BuildingId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<UploadBatchRow>(e =>
            {
                e.ToTable("UploadBatchRows");
                e.HasIndex(r => r.ApartmentHouseId);
                e.HasOne(r => r.Batch).WithMany(b => b.Rows).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.ApartmentHouse).WithMany().HasForeignKey(r => r.ApartmentHouseId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Models.Shared.PlatformSetting>().ToTable("PlatformSettings");
        }

        private static void ConfigureAccessControl(ModelBuilder builder)
        {
            // AccountType is always written by the app; the migration sets it for existing accounts.
            builder.Entity<AppUser>().HasIndex(u => u.AccountType);

            builder.Entity<Models.Security.Permission>().ToTable("Permissions");

            builder.Entity<Models.Security.AccessRole>(e =>
            {
                e.ToTable("AccessRoles");
                // One name per company; platform roles (CompanyId NULL) share one namespace.
                e.HasIndex(r => new { r.CompanyId, r.Name }).IsUnique();
                // Not cascaded from companies: company → profiles → accounts already cascades into UserAccessRoles,
                // and SQL Server allows one cascade path. Deleting a company removes its roles explicitly.
                e.HasOne(r => r.Company).WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Models.Security.AccessRolePermission>(e =>
            {
                e.ToTable("AccessRolePermissions");
                e.HasKey(p => new { p.RoleId, p.PermissionKey });
                e.HasOne(p => p.Role).WithMany(r => r.Permissions).HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Permission).WithMany().HasForeignKey(p => p.PermissionKey).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Models.Security.UserAccessRole>(e =>
            {
                e.ToTable("UserAccessRoles");
                e.HasKey(ur => new { ur.UserId, ur.RoleId });
                e.HasIndex(ur => ur.RoleId);
                e.HasOne(ur => ur.User).WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(ur => ur.Role).WithMany(r => r.Users).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Models.Security.AuditLog>(e =>
            {
                e.ToTable("AuditLogs");
                e.HasIndex(a => a.OccurredOn);
                e.HasIndex(a => new { a.CompanyId, a.OccurredOn });
                e.HasIndex(a => new { a.EntityType, a.EntityId });
            });

            builder.Entity<Models.Security.CompanyStatusChange>(e =>
            {
                e.ToTable("CompanyStatusChanges");
                e.HasIndex(c => new { c.CompanyId, c.ChangedOn });
                e.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Cascade);
            });
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customer portal: referral codes and the friends who registered with them (Models/Referrals.cs)
            builder.Entity<ReferralCode>().HasIndex(c => c.Code).IsUnique();
            builder.Entity<ReferralCode>()
                .HasOne(c => c.Customer).WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Referral>().HasIndex(r => r.ReferredUserId).IsUnique(); // an account is referred once
            builder.Entity<Referral>()
                .HasOne(r => r.ReferralCode).WithMany(c => c.Referrals)
                .HasForeignKey(r => r.ReferralCodeId)
                .OnDelete(DeleteBehavior.Cascade);

            ConfigureAccessControl(builder);
            ConfigureFoundation2(builder);
            ConfigureFoundation3(builder);

            builder.Entity<ApartmentHouseSaleTransaction>()
    .ToTable("ApartmentHouseSaleTransactions");

            builder.Entity<HousePerFloor>()
    .ToTable("HousePerFloors");
            // Keys
            builder.Entity<ApartmentHouse>().HasKey(a => a.Id);

            builder.Entity<Subscriber>().HasKey(a => a.Id);



            builder.Entity<HousePerFloor>().HasKey(a => a.Id);
            builder.Entity<HousePerFloor>().Property(a => a.Id).ValueGeneratedOnAdd();


            builder.Entity<HouseSpecs>().HasKey(a => a.Id);

            builder.Entity<ApartmentPhoto>().HasKey(a => a.PhotoId);
            builder.Entity<ApartmentPhoto>().Property(a => a.PhotoId).ValueGeneratedOnAdd();


            builder.Entity<Building>().HasKey(a => a.Id);
           
            builder.Entity<ApartmentHouseReview>().HasKey(a => a.Id);

            builder.Entity<CompanyFinancialDetail>().HasKey(a => a.Id);

            builder.Entity<CustomerTestimonial>().HasKey(a => a.Id);

            builder.Entity<HouseProject>().HasKey(a => a.ProjectId);

            builder.Entity<HousingCompany>().HasKey(a => a.CompanyId);


            builder.Entity<Notification>().HasKey(n => n.Id);
            builder.Entity<Notification>().Property(n => n.Id).ValueGeneratedOnAdd();


            builder.Entity<CommisionRate>().HasKey(a => a.CommisionRateId);


            builder.Entity<House>().HasKey(a => a.Id);

            builder.Entity<RentInfo>().HasKey(a => a.Id);


            builder.Entity<PartyAddress>().HasKey(a => a.AddressID);

            builder.Entity<IssueOrCompliant>().HasKey(a => a.IssueId);


            builder.Entity<HouseCategory>().HasKey(a => a.Id);

            builder.Entity<HomeOwnerRanking>().HasKey(a => a.RankingId);

            builder.Entity<Customer>().HasKey(a => a.Id);

            builder.Entity<HouseRentPayment>().HasKey(a => a.Id);

            builder.Entity<HouseRentTransaction>().HasKey(a => a.Id);

            builder.Entity<HouseSalePayment>().HasKey(a => a.Id);

            builder.Entity<ApartmentHouseSaleTransaction>().HasKey(a => a.Id);

            builder.Entity<HouseSaleTransaction>().HasKey(a => a.Id);

            builder.Entity<HouseReview>().HasKey(a => a.Id);


            // House Sale Transaction Relationships
            builder.Entity<HouseSalePayment>()
                .HasOne(t => t.Transaction)
                .WithOne(p => p.Payment)
                .HasForeignKey<HouseSalePayment>(t => t.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            
            builder.Entity<Notification>()
                .HasOne(a=>a.Company)
                .WithMany(b=>b.Notifications)
                .HasForeignKey(a=>a.ReceiverCompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Notification>()
                .HasOne(a => a.Admin)
                .WithMany(b => b.Notifications)
                .HasForeignKey(a => a.ReceiverAdminId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Building>()
                .HasOne(t => t.Address)
                .WithMany(p => p.Buildings)
                .HasForeignKey(t => t.AddressId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<HousePerFloor>()
                .HasOne(t => t.Building)
                .WithMany(p => p.TotalHousesPerFloor)
                .HasForeignKey(t => t.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<HouseProject>()
                .HasOne(t => t.Address)
                .WithMany(p => p.Projects)
                .HasForeignKey(t => t.AddressId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<HousingCompany>()
                .HasOne(t => t.Address)
                .WithMany(p => p.RealEstates)
                .HasForeignKey(t => t.AddressId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApartmentHouseSaleTransaction>()
                .HasOne(t => t.HomeOwner)
                .WithMany(o => o.ApartmentSaleTransactions)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.Entity<HouseSaleTransaction>()
                .HasOne(t => t.HomeOwner)
                .WithMany(o => o.SaleTransactions)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.Entity<ApartmentHouseSaleTransaction>()
                .HasOne(t => t.Receiver)
                .WithMany(c => c.ApartmentPurchaseTransactions)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.Entity<HouseSaleTransaction>()
                .HasOne(t => t.Receiver)
                .WithMany(c => c.PurchaseTransactions)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();


            builder.Entity<ApartmentHouseSaleTransaction>()
                .HasOne(t => t.ApartmentHouse)
                .WithMany(h => h.SaleTransactions)
                .HasForeignKey(t => t.ApartmentHouseId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.Entity<HouseSaleTransaction>()
               .HasOne(t => t.House)
               .WithMany(h => h.SaleTransactions)
               .HasForeignKey(t => t.HouseId)
               .OnDelete(DeleteBehavior.Restrict)
               .IsRequired();
            // House Rent Transaction Relationships
            builder.Entity<HouseRentPayment>()
                .HasOne(t => t.Transaction)
                .WithOne(p => p.Payment)
                .HasForeignKey<HouseRentPayment>(t => t.TransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.Entity<HouseRentTransaction>()
                .HasOne(t => t.HomeOwner)
                .WithMany(o => o.HouseRentTransactions)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.Entity<HouseRentTransaction>()
                .HasOne(t => t.Receiver)
                .WithMany(c => c.RentTransactions)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.Entity<HouseRentTransaction>()
                .HasOne(t => t.House)
                .WithMany(h => h.RentTransactions)
                .HasForeignKey(t => t.HouseId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // House Relationships
            builder.Entity<House>()
                .HasOne(h => h.HouseCategory)
                .WithMany(c => c.Houses)
                .HasForeignKey(h => h.HouseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<House>()
                .HasOne(h => h.HouseOwner)
                .WithMany(o => o.Houses)
                .HasForeignKey(h => h.HouseOwnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.Entity<ApartmentHouse>()
               .HasOne(h => h.ContactPerson)
               .WithMany(o => o.ApartmentHouses)
               .HasForeignKey(h => h.ContactPersonId)
               .OnDelete(DeleteBehavior.Restrict)
               .IsRequired(false);
            builder.Entity<House>()
                .HasOne(h => h.HouseAddress)
                .WithMany(o => o.Houses)
                .HasForeignKey(h => h.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApartmentHouse>()
                .HasOne(h => h.HouseAddress)
                .WithMany(o => o.ApartmentHouses)
                .HasForeignKey(h => h.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApartmentHouse>()
                .HasMany(h => h.HouseSpecs)
                .WithOne(o => o.House)
                .HasForeignKey(h => h.HouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApartmentHouse>()
                .HasMany(h => h.Photos)
                .WithOne(o => o.House)
                .HasForeignKey(o => o.HouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer Relationships
            builder.Entity<Customer>()
                .HasOne(c => c.Address)
                .WithMany(a => a.Customers)
                .HasForeignKey(c => c.AddressId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<Customer>()
                .HasOne(c => c.Ranking)
                .WithMany(r => r.HouseHolder)
                .HasForeignKey(c => c.RankingId)
                .OnDelete(DeleteBehavior.Restrict);

            
            // House Receiver Relationships
            builder.Entity<Customer>()
                .HasOne(r => r.Address)
                .WithMany(a => a.Customers)
                .HasForeignKey(r => r.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            
            builder.Entity<AppUser>()
                   .HasOne(u => u.Customer)
                   .WithOne(u => u.CustomerUserAccount)
                   .HasForeignKey<AppUser>(u => u.CustomerId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);  // ✅ Important!
            builder.Entity<House>()
               .HasMany(r => r.IssuesOrReportings)
               .WithOne(u => u.House)
               .HasForeignKey(r => r.HouseId)
               .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<House>()
                .HasOne(r => r.HouseRentInfo)
                .WithOne(u => u.House)
                .HasForeignKey<RentInfo>(r => r.HouseId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<HouseReview>()
        .HasOne(hr => hr.House)
        .WithMany(h => h.Reviews)
        .HasForeignKey(hr => hr.HouseId)
        .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<ApartmentHouseReview>()
        .HasOne(hr => hr.ApartmentHouse)
        .WithMany(h => h.Reviews)
        .HasForeignKey(hr => hr.HouseId)
        .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<HousingCompany>()
                    .HasMany(c => c.ContactPerson)
                    .WithOne(cp => cp.Company)
                    .HasForeignKey(cp => cp.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false); // Example of cascade delete
            builder.Entity<ApartmentHouseReview>()
                    .HasOne(c => c.ReviewerCustomer)
                    .WithMany(cp => cp.ApartmentReviews)
                    .HasForeignKey(cp => cp.ReviewerCustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Customer>()
                    .HasOne(c => c.Address)
                    .WithMany()
                    .HasForeignKey(cp => cp.AddressId)
                    .OnDelete(DeleteBehavior.NoAction); // Example of cascade delete
            builder.Entity<HousingCompany>()
                .HasMany(c => c.FinancialDetails)
                .WithOne(fd => fd.Company)
                .HasForeignKey(fd => fd.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<HousingCompany>()
                .HasMany(c => c.Projects)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.HousingCompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // HouseProject Relationships
            builder.Entity<HouseProject>()
                .HasMany(p => p.Buildings)
                .WithOne(b => b.Project)
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            // Building Relationships (assuming ApartmentHouse is related to Building)
            builder.Entity<ApartmentHouse>()
                .HasOne(b => b.Building) // Assuming a Building can have many ApartmentHouses
                .WithMany(a => a.Houses)
                .HasForeignKey(b => b.BuildingId)
                .OnDelete(DeleteBehavior.NoAction);

            // CustomerTestimonial Relationships
            builder.Entity<CustomerTestimonial>()
                .HasOne(t => t.Customer)
                .WithMany(c => c.Testimonials)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // HouseReview Relationships (uncomment and configure)
            builder.Entity<HouseReview>()
                .HasOne(hr => hr.ReviewerCustomer)
                .WithMany(c => c.Reviews) // Add a HouseReviews collection to Customer entity
                .HasForeignKey(hr => hr.ReviewerCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApartmentHouseReview>()
                .HasOne(hr => hr.ReviewerCustomer)
                .WithMany()
                .HasForeignKey(hr => hr.ReviewerCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<HouseProject>()
                .HasMany(a => a.Photos)
                .WithOne()
                .HasForeignKey(a=>a.HouseId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Building>()
                .HasMany(a => a.Photos)
                .WithOne()
                .HasForeignKey(a => a.HouseId)
                 .OnDelete(DeleteBehavior.NoAction); 
        }
    }
}
