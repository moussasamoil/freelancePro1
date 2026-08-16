using Crm_LotusBlue.Models;
using lotus_blue.Models;
using lotus_blue.Models.WebHooksViewModel;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<DeliveryCompany> DeliveryCompanies { get; set; }
        public DbSet<Crm_LotusBlue.Models.EmployeeActivityHourlyLog> EmployeeActivityHourlyLogs { get; set; }
        public DbSet<ManufacturingCompany> ManufacturingCompanies { get; set; }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<ManufacturingCompanyMainWarehouse> ManufacturingCompanyMainWarehouses { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }

        public DbSet<SalesIndicator> SalesIndicators { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<OrderInvestigationApproval> OrderInvestigationApprovals { get; set; }

        public DbSet<EmployeeTransaction> EmployeeTransactions { get; set; }

        public DbSet<ExchangeRate> ExchangeRates { get; set; }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderInvestigationOpening> OrderInvestigationOpenings { get; set; }
        public DbSet<OrderEditHistory> OrderEditHistories { get; set; }

        public DbSet<DeliveryCompanyPrice> DeliveryCompanyPrices { get; set; }


        public DbSet<OrderWarehouse> OrderWarehouses { get; set; }

        public DbSet<EmployeeManufacturingCompany> EmployeeManufacturingCompany { get; set; } // Add this line


        public DbSet<OrderWarehouseEditHistory> OrderWarehouseEditHistories { get; set; }

        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

        public DbSet<OrderStatusUpdateSelection> OrderStatusUpdateSelections { get; set; }

        public DbSet<OrderReport> OrderReports { get; set; }

        public DbSet<OrderBonusConfiguration> OrderBonusConfigurations { get; set; }

        public DbSet<OrderUserChangeHistory> OrderUserChangeHistories { get; set; }

        public DbSet<Crm_LotusBlue.Models.EmployeeActivityLog> EmployeeActivityLogs { get; set; }
        public DbSet<WarehouseEditHistory> WarehouseEditHistories { get; set; }

        public DbSet<MainProduct> MainProducts { get; set; }
        public DbSet<EmployeeWorkShift> EmployeeWorkShifts { get; set; }
        public DbSet<EmployeeAttendanceLog> EmployeeAttendanceLogs { get; set; }
        public DbSet<OrderFromCommentsHistory> OrderFromCommentsHistories { get; set; }

        public DbSet<MainWarehouse> MainWarehouses { get; set; }
        public DbSet<SubWarehouse> SubWarehouses { get; set; }

        public DbSet<CountryMinimumPrice> CountryMinimumPrices { get; set; }

        public DbSet<ProductMinimumSellingPrice> ProductMinimumSellingPrices { get; set; }

        public DbSet<OrderReportOrder> OrderReportOrders { get; set; }
        public DbSet<EmployeePaymentSummary> EmployeePaymentSummaries { get; set; }
        public DbSet<EmployeeBonusRate> EmployeeBonusRates { get; set; }

        public DbSet<EmployeeBonusPayment> EmployeeBonusPayments { get; set; }

        public DbSet<SocialMediaConversation> SocialMediaConversations { get; set; }
        public DbSet<SocialMediaMessage> SocialMediaMessages { get; set; }

        public DbSet<PotentialOrder> PotentialOrders { get; set; }

        public DbSet<Lead> Leads { get; set; }

        public DbSet<Campaign> Campaigns { get; set; }

        public DbSet<FraudAttemptLog> FraudAttemptLogs { get; set; }

        public DbSet<OrderPost> OrderPosts { get; set; }
        public DbSet<OrderPostImage> OrderPostImages { get; set; }

        public DbSet<UserSwitchGroup> UserSwitchGroups { get; set; }
        public DbSet<UserSwitchGroupMember> UserSwitchGroupMembers { get; set; }

        public DbSet<EmployeeError> EmployeeErrors { get; set; }
        public DbSet<EmployeeErrorEditHistory> EmployeeErrorEditHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            // Employee Errors
            modelBuilder.Entity<EmployeeError>()
                .HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeError>()
                .HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_EmployeeErrors_EmployeeId");

            modelBuilder.Entity<EmployeeError>()
                .HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_EmployeeErrors_IsDeleted");

            modelBuilder.Entity<EmployeeError>()
                .HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_EmployeeErrors_CreatedAt");

            modelBuilder.Entity<EmployeeErrorEditHistory>()
                .HasOne(h => h.EmployeeError)
                .WithMany()
                .HasForeignKey(h => h.EmployeeErrorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeErrorEditHistory>()
                .HasIndex(h => h.EmployeeErrorId)
                .HasDatabaseName("IX_EmployeeErrorEditHistories_EmployeeErrorId");

            modelBuilder.Entity<EmployeeErrorEditHistory>()
                .HasIndex(h => h.CreatedAt)
                .HasDatabaseName("IX_EmployeeErrorEditHistories_CreatedAt");


            // ManufacturingCompany -> multiple MainWarehouses
            modelBuilder.Entity<ManufacturingCompanyMainWarehouse>()
                .HasIndex(x => new { x.ManufacturingCompanyId, x.MainWarehouseId })
                .IsUnique()
                .HasDatabaseName("IX_ManufacturingCompanyMainWarehouses_Company_Warehouse");

            modelBuilder.Entity<ManufacturingCompanyMainWarehouse>()
                .HasOne(x => x.ManufacturingCompany)
                .WithMany(x => x.ManufacturingCompanyMainWarehouses)
                .HasForeignKey(x => x.ManufacturingCompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ManufacturingCompanyMainWarehouse>()
                .HasOne(x => x.MainWarehouse)
                .WithMany()
                .HasForeignKey(x => x.MainWarehouseId)
                .OnDelete(DeleteBehavior.Cascade);


            // User account switching groups
            modelBuilder.Entity<UserSwitchGroup>()
                .HasIndex(g => g.Name)
                .HasDatabaseName("IX_UserSwitchGroups_Name");

            modelBuilder.Entity<UserSwitchGroupMember>()
                .HasOne(m => m.UserSwitchGroup)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.UserSwitchGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSwitchGroupMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserSwitchGroupMember>()
                .HasIndex(m => new { m.UserSwitchGroupId, m.ApplicationUserId })
                .IsUnique()
                .HasDatabaseName("IX_UserSwitchGroupMembers_Group_User");

            modelBuilder.Entity<UserSwitchGroupMember>()
                .HasIndex(m => m.ApplicationUserId)
                .HasDatabaseName("IX_UserSwitchGroupMembers_ApplicationUserId");

            // OrderPost (discussion thread post on an order: problem report, edit-note, or order-note).
            modelBuilder.Entity<OrderPost>()
                .HasOne(p => p.Order)
                .WithMany()
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderPost>()
                .HasOne(p => p.AuthorUser)
                .WithMany()
                .HasForeignKey(p => p.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderPost>()
                .HasIndex(p => new { p.OrderId, p.Type, p.CreatedAt });

            modelBuilder.Entity<OrderPost>()
                .HasIndex(p => new { p.Type, p.CreatedAt });

            modelBuilder.Entity<OrderPost>()
                .HasIndex(p => p.AuthorUserId);

            modelBuilder.Entity<OrderPostImage>()
                .HasOne(i => i.OrderPost)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.OrderPostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the composite key for OrderWarehouse
            modelBuilder.Entity<OrderWarehouse>()
                .HasKey(ow => new { ow.OrderId, ow.WarehouseId });

            // Configure the many-to-many relationship between Order and Warehouse via OrderWarehouse
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderWarehouses)
                .WithOne(ow => ow.Order)
                .HasForeignKey(ow => ow.OrderId);

            modelBuilder.Entity<Warehouse>()
                .HasMany(w => w.OrderWarehouses)
                .WithOne(ow => ow.Warehouse)
                .HasForeignKey(ow => ow.WarehouseId);

            // If OrderWarehouse has additional properties such as Amount, configure them as well
            modelBuilder.Entity<OrderWarehouse>()
                .Property(ow => ow.Amount)
                .IsRequired();

            // Optionally, configure other properties and relationships
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.LastEditedDate);


            modelBuilder.Entity<EmployeeManufacturingCompany>()
     .HasKey(emc => new { emc.ApplicationUserId, emc.ManufacturingCompanyId });

            modelBuilder.Entity<EmployeeManufacturingCompany>()
                .HasOne(emc => emc.Employee)
                .WithMany()  // Assuming no navigation property back to EmployeeManufacturingCompany
                .HasForeignKey(emc => emc.EmployeeId);  // Use EmployeeId here

            modelBuilder.Entity<EmployeeManufacturingCompany>()
                .HasOne(emc => emc.ManufacturingCompany)
                .WithMany(mc => mc.EmployeeManufacturingCompanies)
                .HasForeignKey(emc => emc.ManufacturingCompanyId);

            modelBuilder.Entity<EmployeeManufacturingCompany>()
                .HasOne(emc => emc.ApplicationUser)
                .WithMany()  // Adjust this based on your ApplicationUser relationships
                .HasForeignKey(emc => emc.ApplicationUserId);

            modelBuilder.Entity<OrderReportOrder>()
         .HasKey(oro => new { oro.OrderReportId, oro.OrderId });

            modelBuilder.Entity<OrderReportOrder>()
                .HasOne(oro => oro.OrderReport)
                .WithMany(or => or.OrderReportOrders)
                .HasForeignKey(oro => oro.OrderReportId);

            modelBuilder.Entity<OrderReportOrder>()
                .HasOne(oro => oro.Order)
                .WithMany(o => o.OrderReportOrders)
                .HasForeignKey(oro => oro.OrderId);
            // index
            modelBuilder.Entity<Order>()
            .HasIndex(o => o.ApplicationUserId)
            .HasDatabaseName("IX_Orders_ApplicationUserId");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.InstantAddedDate)
                .HasDatabaseName("IX_Orders_InstantAddedDate");

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.InstantAddedDate, o.FixedOrderDate })
                .HasDatabaseName("IX_Orders_InstantAddedDate_FixedOrderDate");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.Country)
                .HasDatabaseName("IX_Orders_Country");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ManufacturingCompanyId)
                .HasDatabaseName("IX_Orders_ManufacturingCompanyId");

            // Configure one-to-many relationship between Conversation and Message
            modelBuilder.Entity<SocialMediaConversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.SocialMediaConversationId);

            modelBuilder.Entity<ProductMinimumSellingPrice>()
                .HasIndex(x => new { x.Country, x.ManufacturingCompanyId, x.MainWarehouseId })
                .IsUnique();

            // Server-side temporary selections for order status update pages
            modelBuilder.Entity<OrderStatusUpdateSelection>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.FailureReason)
                    .HasMaxLength(500);

                entity.Property(x => x.SelectedByUserId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.SelectedByName)
                    .HasMaxLength(250);

                entity.HasOne(x => x.Order)
                    .WithMany()
                    .HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => x.OrderId)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1")
                    .HasDatabaseName("IX_OrderStatusUpdateSelections_OrderId_Active");

                entity.HasIndex(x => new { x.IsActive, x.TargetStatus })
                    .HasDatabaseName("IX_OrderStatusUpdateSelections_IsActive_TargetStatus");

                entity.HasIndex(x => x.ExpiresAt)
                    .HasDatabaseName("IX_OrderStatusUpdateSelections_ExpiresAt");
            });

        }




        // Other configurations...





    }



}
