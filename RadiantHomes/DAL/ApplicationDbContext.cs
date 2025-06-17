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
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
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


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<ApartmentHouseSaleTransaction>()
    .ToTable("ApartmentHouseSaleTransactions");

            builder.Entity<HousePerFloor>()
    .ToTable("HousePerFloors");
            // Keys
            builder.Entity<ApartmentHouse>().HasKey(a => a.Id);
            builder.Entity<ApartmentHouse>().Property(a => a.Id).HasDefaultValueSql("NEWID()");
            builder.Entity<HousePerFloor>().HasKey(a => a.Id);
            builder.Entity<HousePerFloor>().Property(a => a.Id).ValueGeneratedOnAdd();


            builder.Entity<HouseSpecs>().HasKey(a => a.Id);
            builder.Entity<HouseSpecs>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<ApartmentPhoto>().HasKey(a => a.PhotoId);
            builder.Entity<ApartmentPhoto>().Property(a => a.PhotoId).ValueGeneratedOnAdd();


            builder.Entity<Building>().HasKey(a => a.Id);
            builder.Entity<Building>().Property(a => a.Id).HasDefaultValueSql("NEWID()");
           
            builder.Entity<ApartmentHouseReview>().HasKey(a => a.Id);
            builder.Entity<ApartmentHouseReview>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<CompanyFinancialDetail>().HasKey(a => a.Id);
            builder.Entity<CompanyFinancialDetail>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<CustomerTestimonial>().HasKey(a => a.Id);
            builder.Entity<CustomerTestimonial>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseProject>().HasKey(a => a.ProjectId);
            builder.Entity<HouseProject>().Property(a => a.ProjectId).HasDefaultValueSql("NEWID()");

            builder.Entity<HousingCompany>().HasKey(a => a.CompanyId);
            builder.Entity<HousingCompany>().Property(a => a.CompanyId).HasDefaultValueSql("NEWID()");


            builder.Entity<Notification>().HasKey(n => n.Id);
            builder.Entity<Notification>().Property(n => n.Id).ValueGeneratedOnAdd();


            builder.Entity<CommisionRate>().HasKey(a => a.CommisionRateId);
            builder.Entity<CommisionRate>().Property(a => a.CommisionRateId).HasDefaultValueSql("NEWID()");


            builder.Entity<House>().HasKey(a => a.Id);
            builder.Entity<House>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<RentInfo>().HasKey(a => a.Id);
            builder.Entity<RentInfo>().Property(a => a.Id).HasDefaultValueSql("NEWID()");


            builder.Entity<PartyAddress>().HasKey(a => a.AddressID);
            builder.Entity<PartyAddress>().Property(a => a.AddressID).HasDefaultValueSql("NEWID()");

            builder.Entity<IssueOrCompliant>().HasKey(a => a.IssueId);
            builder.Entity<IssueOrCompliant>().Property(a => a.IssueId).HasDefaultValueSql("NEWID()");


            builder.Entity<HouseCategory>().HasKey(a => a.Id);
            builder.Entity<HouseCategory>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HomeOwnerRanking>().HasKey(a => a.RankingId);
            builder.Entity<HomeOwnerRanking>().Property(a => a.RankingId).HasDefaultValueSql("NEWID()");

            builder.Entity<Customer>().HasKey(a => a.Id);
            builder.Entity<Customer>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseRentPayment>().HasKey(a => a.Id);
            builder.Entity<HouseRentPayment>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseRentTransaction>().HasKey(a => a.Id);
            builder.Entity<HouseRentTransaction>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseSalePayment>().HasKey(a => a.Id);
            builder.Entity < HouseSalePayment>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<ApartmentHouseSaleTransaction>().HasKey(a => a.Id);
            builder.Entity<ApartmentHouseSaleTransaction>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseSaleTransaction>().HasKey(a => a.Id);
            builder.Entity<HouseSaleTransaction>().Property(a => a.Id).HasDefaultValueSql("NEWID()");

            builder.Entity<HouseReview>().HasKey(a => a.Id);
            builder.Entity<HouseReview>().Property(a => a.Id).HasDefaultValueSql("NEWID()");


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
