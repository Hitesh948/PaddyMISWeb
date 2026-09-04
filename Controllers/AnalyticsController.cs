using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";


        // =========================================================
        // MAIN ANALYTICS PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Index(
            DateTime? fromDate,
            DateTime? toDate,
            string? type)
        {
            DateTime selectedFromDate =
                fromDate?.Date
                ?? new DateTime(
                    DateTime.Today.Year,
                    DateTime.Today.Month,
                    1);

            DateTime selectedToDate =
                toDate?.Date
                ?? DateTime.Today;

            type = string.IsNullOrWhiteSpace(type)
                ? "All"
                : type.Trim();


            if (selectedFromDate > selectedToDate)
            {
                selectedFromDate =
                    selectedToDate;
            }


            if (type != "All" &&
                type != "Purchase" &&
                type != "Dispatch")
            {
                type = "All";
            }


            DateTime endDateExclusive =
                selectedToDate.AddDays(1);


            int totalPurchase = 0;
            int totalDispatch = 0;
            int netTrolleys = 0;
            int activeDumps = 0;


            DataTable dumpWiseData =
                new DataTable();

            DataTable dateWiseData =
                new DataTable();


            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                connection.Open();


                // =================================================
                // TOTAL PURCHASE
                // =================================================

                string purchaseQuery = @"
                    SELECT
                        ISNULL(
                            SUM(
                                ISNULL(d.DayTrolley, 0)
                                +
                                ISNULL(d.NightTrolley, 0)
                            ),
                            0
                        )
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE
                        d.EntryDate >= @FromDate
                        AND d.EntryDate < @ToDate
                        AND g.GroupType = 'Purchase';
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           purchaseQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@FromDate",
                        SqlDbType.Date).Value =
                        selectedFromDate;

                    command.Parameters.Add(
                        "@ToDate",
                        SqlDbType.Date).Value =
                        endDateExclusive;


                    totalPurchase =
                        Convert.ToInt32(
                            command.ExecuteScalar());
                }


                // =================================================
                // TOTAL DISPATCH
                // =================================================

                string dispatchQuery = @"
                    SELECT
                        ISNULL(
                            SUM(
                                ISNULL(d.DayTrolley, 0)
                                +
                                ISNULL(d.NightTrolley, 0)
                            ),
                            0
                        )
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE
                        d.EntryDate >= @FromDate
                        AND d.EntryDate < @ToDate
                        AND g.GroupType = 'Dispatch';
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           dispatchQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@FromDate",
                        SqlDbType.Date).Value =
                        selectedFromDate;

                    command.Parameters.Add(
                        "@ToDate",
                        SqlDbType.Date).Value =
                        endDateExclusive;


                    totalDispatch =
                        Convert.ToInt32(
                            command.ExecuteScalar());
                }


                // =================================================
                // NET TROLLEYS
                // =================================================

                netTrolleys =
                    totalPurchase -
                    totalDispatch;


                // =================================================
                // ACTIVE DUMPS
                // =================================================

                string activeDumpsQuery = @"
                    SELECT COUNT(*)
                    FROM Groups
                    WHERE IsActive = 1;
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           activeDumpsQuery,
                           connection))
                {
                    activeDumps =
                        Convert.ToInt32(
                            command.ExecuteScalar());
                }


                // =================================================
                // DUMP-WISE ANALYSIS
                // =================================================

                string dumpWiseQuery = @"
                    SELECT
                        g.GroupName AS DumpName,

                        SUM(
                            ISNULL(d.DayTrolley, 0)
                        ) AS DayTrolleys,

                        SUM(
                            ISNULL(d.NightTrolley, 0)
                        ) AS NightTrolleys,

                        SUM(
                            ISNULL(d.DayTrolley, 0)
                            +
                            ISNULL(d.NightTrolley, 0)
                        ) AS TotalTrolleys

                    FROM DailyTrolleyDetails d

                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID

                    WHERE
                        d.EntryDate >= @FromDate
                        AND d.EntryDate < @ToDate
                ";


                if (type == "Purchase")
                {
                    dumpWiseQuery += @"
                        AND g.GroupType = 'Purchase'
                    ";
                }
                else if (type == "Dispatch")
                {
                    dumpWiseQuery += @"
                        AND g.GroupType = 'Dispatch'
                    ";
                }


                dumpWiseQuery += @"

                    GROUP BY
                        g.GroupName

                    ORDER BY
                        TotalTrolleys DESC,
                        g.GroupName;
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           dumpWiseQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@FromDate",
                        SqlDbType.Date).Value =
                        selectedFromDate;

                    command.Parameters.Add(
                        "@ToDate",
                        SqlDbType.Date).Value =
                        endDateExclusive;


                    using SqlDataAdapter adapter =
                        new SqlDataAdapter(command);

                    adapter.Fill(
                        dumpWiseData);
                }


                // =================================================
                // DATE-WISE ANALYSIS
                // =================================================

                string dateWiseQuery = @"
                    SELECT
                        CAST(
                            d.EntryDate AS DATE
                        ) AS EntryDate,

                        SUM(
                            ISNULL(d.DayTrolley, 0)
                        ) AS DayTrolleys,

                        SUM(
                            ISNULL(d.NightTrolley, 0)
                        ) AS NightTrolleys,

                        SUM(
                            ISNULL(d.DayTrolley, 0)
                            +
                            ISNULL(d.NightTrolley, 0)
                        ) AS TotalTrolleys

                    FROM DailyTrolleyDetails d

                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID

                    WHERE
                        d.EntryDate >= @FromDate
                        AND d.EntryDate < @ToDate
                ";


                if (type == "Purchase")
                {
                    dateWiseQuery += @"
                        AND g.GroupType = 'Purchase'
                    ";
                }
                else if (type == "Dispatch")
                {
                    dateWiseQuery += @"
                        AND g.GroupType = 'Dispatch'
                    ";
                }


                dateWiseQuery += @"

                    GROUP BY
                        CAST(d.EntryDate AS DATE)

                    ORDER BY
                        EntryDate;
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           dateWiseQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@FromDate",
                        SqlDbType.Date).Value =
                        selectedFromDate;

                    command.Parameters.Add(
                        "@ToDate",
                        SqlDbType.Date).Value =
                        endDateExclusive;


                    using SqlDataAdapter adapter =
                        new SqlDataAdapter(command);

                    adapter.Fill(
                        dateWiseData);
                }
            }


            // =====================================================
            // VIEWBAG
            // =====================================================

            ViewBag.FromDate =
                selectedFromDate.ToString(
                    "yyyy-MM-dd");

            ViewBag.ToDate =
                selectedToDate.ToString(
                    "yyyy-MM-dd");

            ViewBag.SelectedType =
                type;

            ViewBag.TotalPurchase =
                totalPurchase;

            ViewBag.TotalDispatch =
                totalDispatch;

            ViewBag.NetTrolleys =
                netTrolleys;

            ViewBag.ActiveDumps =
                activeDumps;


            return View(
                Tuple.Create(
                    dumpWiseData,
                    dateWiseData));
        }
    }
}