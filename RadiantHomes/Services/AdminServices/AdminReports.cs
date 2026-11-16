using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Services.AdminServices
{
    /// <summary>The reports offered on Admin/Reports. The page, the PDF and the Excel download are built from the same <see cref="AdminReport"/>.</summary>
    public static class AdminReports
    {
        public const string SaleTransactions = "ApartmentHouseSaleTransactions";
        public const string AllApartmentHouses = "AllApartmentHouses";
        public const string AllCustomers = "AllCustomers";
        public const string AllUsers = "AllUsers";
        public const string HousingCompanies = "HousingCompanies";
        public const string SoldHousesByDateRange = "SoldHousesByDateRange";

        /// <summary>
        /// Builds the report, or returns null with <paramref name="error"/> set when it can't be built
        /// (unknown report, missing/invalid date range, report not available).
        /// </summary>
        public static async Task<(AdminReport? Report, string? Error)> BuildAsync(
            ApplicationDbContext context, string? reportType, DateTime? startDate, DateTime? endDate)
        {
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            switch (reportType)
            {
                case SaleTransactions:
                {
                    var report = new AdminReport
                    {
                        Key = SaleTransactions,
                        Title = "Apartment/House Sale Transactions",
                        Icon = "bi-cash-coin",
                        FileName = $"HouseSalesTransactionsReport_{stamp}",
                        SheetName = "Sale transactions",
                        Columns = new[]
                        {
                            new ReportColumn("Transaction date", ReportColumnKind.Date),
                            new ReportColumn("House"),
                            new ReportColumn("House number"),
                            new ReportColumn("Buyer"),
                            new ReportColumn("Seller"),
                            new ReportColumn("Status"),
                            new ReportColumn("Notes"),
                        },
                    };
                    var rows = await context.ApartmentHouseSaleTransactions
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            t.TransactionDate,
                            House = t.ApartmentHouse.Title,
                            t.ApartmentHouse.HouseNumber,
                            BuyerFirst = t.Receiver.FirstName, BuyerLast = t.Receiver.LastName,
                            SellerFirst = t.HomeOwner.FirstName, SellerLast = t.HomeOwner.LastName,
                            t.Status,
                            t.Notes,
                        })
                        .ToListAsync();
                    foreach (var r in rows)
                    {
                        report.Rows.Add(new object?[]
                        {
                            r.TransactionDate, r.House, r.HouseNumber, FullName(r.BuyerFirst, r.BuyerLast),
                            FullName(r.SellerFirst, r.SellerLast), r.Status.ToString(), r.Notes,
                        });
                    }
                    return (report, null);
                }

                case AllApartmentHouses:
                {
                    var report = new AdminReport
                    {
                        Key = AllApartmentHouses,
                        Title = "All Apartment Houses",
                        Icon = "bi-buildings",
                        FileName = $"AllApartmentHousesReport_{stamp}",
                        SheetName = "Apartment houses",
                        Columns = HouseColumns(),
                    };
                    var rows = await context.ApartmentHouses
                        .OrderBy(h => h.Building!.Project!.Company!.Name)
                        .ThenBy(h => h.Building!.BuildingName)
                        .ThenBy(h => h.Title)
                        .Select(h => new
                        {
                            h.Title, h.HouseNumber,
                            Building = h.Building!.BuildingName,
                            Company = h.Building.Project!.Company!.Name,
                            h.Bedrooms, h.SalePrice, h.Currency, h.Status,
                        })
                        .ToListAsync();
                    foreach (var r in rows)
                    {
                        report.Rows.Add(new object?[] { r.Title, r.HouseNumber, r.Building, r.Company, r.Bedrooms, new Money(r.SalePrice, r.Currency), r.Status });
                    }
                    return (report, null);
                }

                case AllCustomers:
                {
                    var report = new AdminReport
                    {
                        Key = AllCustomers,
                        Title = "All Customers",
                        Icon = "bi-people",
                        FileName = $"AllCustomersReport_{stamp}",
                        SheetName = "Customers",
                        Columns = new[]
                        {
                            new ReportColumn("First name"),
                            new ReportColumn("Last name"),
                            new ReportColumn("Email"),
                            new ReportColumn("Phone"),
                            new ReportColumn("Registration date", ReportColumnKind.Date),
                        },
                    };
                    var rows = await context.UserProfiles
                        .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                        .Select(c => new { c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.ApplicationDate })
                        .ToListAsync();
                    foreach (var r in rows)
                    {
                        report.Rows.Add(new object?[] { r.FirstName, r.LastName, r.Email, r.PhoneNumber, r.ApplicationDate });
                    }
                    return (report, null);
                }

                case HousingCompanies:
                {
                    var report = new AdminReport
                    {
                        Key = HousingCompanies,
                        Title = "Housing Companies",
                        Icon = "bi-building",
                        FileName = $"HousingCompaniesReport_{stamp}",
                        SheetName = "Housing companies",
                        Columns = new[]
                        {
                            new ReportColumn("Real estate name"),
                            new ReportColumn("Total houses", ReportColumnKind.Integer),
                            new ReportColumn("Website"),
                            new ReportColumn("Address"),
                        },
                    };
                    var houseCounts = await context.ApartmentHouses
                        .Where(a => a.Building != null && a.Building.Project != null)
                        .GroupBy(a => a.Building!.Project!.HousingCompanyId)
                        .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.CompanyId, x => x.Count);
                    var companies = await context.HousingCompanies
                        .Include(c => c.Address)
                        .OrderBy(c => c.Name)
                        .ToListAsync();
                    foreach (var c in companies)
                    {
                        report.Rows.Add(new object?[]
                        {
                            c.Name, houseCounts.GetValueOrDefault(c.CompanyId), c.Website,
                            ShortAddress(c.Address),
                        });
                    }
                    return (report, null);
                }

                case SoldHousesByDateRange:
                {
                    if (!startDate.HasValue || !endDate.HasValue)
                        return (null, "Start Date and End Date are required for the Sold Houses (by Date Range) report.");
                    if (startDate.Value.Date > endDate.Value.Date)
                        return (null, "The Start Date must be on or before the End Date.");

                    var from = startDate.Value.Date;
                    var toExclusive = endDate.Value.Date.AddDays(1); // the end date is included
                    var report = new AdminReport
                    {
                        Key = SoldHousesByDateRange,
                        Title = "Sold Houses (by Date Range)",
                        Icon = "bi-calendar-range",
                        FileName = $"SoldHousesByDateRangeReport_{from:yyyyMMdd}-{endDate.Value:yyyyMMdd}",
                        SheetName = "Sold houses",
                        Subtitle = $"Transactions from {from:yyyy-MM-dd} to {endDate.Value:yyyy-MM-dd}",
                        Columns = new[]
                        {
                            new ReportColumn("Transaction date", ReportColumnKind.Date),
                            new ReportColumn("House"),
                            new ReportColumn("House number"),
                            new ReportColumn("Building"),
                            new ReportColumn("Sale price", ReportColumnKind.Amount),
                            new ReportColumn("Buyer"),
                            new ReportColumn("Status"),
                        },
                    };
                    var rows = await context.ApartmentHouseSaleTransactions
                        .Where(t => t.TransactionDate >= from && t.TransactionDate < toExclusive)
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            t.TransactionDate,
                            House = t.ApartmentHouse.Title,
                            t.ApartmentHouse.HouseNumber,
                            Building = t.ApartmentHouse.Building!.BuildingName,
                            t.ApartmentHouse.SalePrice,
                            t.ApartmentHouse.Currency,
                            BuyerFirst = t.Receiver.FirstName, BuyerLast = t.Receiver.LastName,
                            t.Status,
                        })
                        .ToListAsync();
                    foreach (var r in rows)
                    {
                        report.Rows.Add(new object?[]
                        {
                            r.TransactionDate, r.House, r.HouseNumber, r.Building, new Money(r.SalePrice, r.Currency),
                            FullName(r.BuyerFirst, r.BuyerLast), r.Status.ToString(),
                        });
                    }
                    return (report, null);
                }

                case AllUsers:
                    return (null, "The 'All Users (Agents)' report isn't available yet. Please select another report type.");

                default:
                    return (null, "Please select a valid report type to generate.");
            }
        }

        private static ReportColumn[] HouseColumns() => new[]
        {
            new ReportColumn("House"),
            new ReportColumn("House number"),
            new ReportColumn("Building"),
            new ReportColumn("Real estate"),
            new ReportColumn("Bedrooms", ReportColumnKind.Integer),
            new ReportColumn("Sale price", ReportColumnKind.Amount),
            new ReportColumn("Status"),
        };

        private static string FullName(string? first, string? last) => $"{first} {last}".Trim();

        // "Street, Woreda, Sub-city, City": short enough for a table column (the full form adds region, country, P.O. box and landmarks)
        private static string? ShortAddress(PartyAddress? address) =>
            address == null
                ? null
                : string.Join(", ", new[] { address.Street, address.Woreda, address.SubCity, address.City }
                    .Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }
}
