using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";

        public IActionResult Index()
        {
            DateTime today = DateTime.Today;

            // Monday = start of week
            DateTime weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            // If today is Sunday, calculate Monday of current week correctly
            if (today.DayOfWeek == DayOfWeek.Sunday)
            {
                weekStart = today.AddDays(-6);
            }

            DateTime weekEnd = weekStart.AddDays(6);

            DataTable weeklyData = new DataTable();

            int todayTrolley = 0;
            int todayEntries = 0;
            int weekTrolley = 0;
            int weekEntries = 0;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                // Today's trolley and entries
                string todayQuery = @"
                    SELECT
                        ISNULL(SUM(ISNULL(d.DayTrolley, 0) + ISNULL(d.NightTrolley, 0)), 0) AS TodayTrolley,
                        COUNT(*) AS TodayEntries
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE d.EntryDate = @Today
                      AND g.GroupType = 'Purchase'
                      AND g.IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(todayQuery, con))
                {
                    cmd.Parameters.AddWithValue("@Today", today);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            todayTrolley = Convert.ToInt32(reader["TodayTrolley"]);
                            todayEntries = Convert.ToInt32(reader["TodayEntries"]);
                        }
                    }
                }

                // Weekly trolley and entries
                string weekQuery = @"
                    SELECT
                        ISNULL(SUM(ISNULL(d.DayTrolley, 0) + ISNULL(d.NightTrolley, 0)), 0) AS WeekTrolley,
                        COUNT(*) AS WeekEntries
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE d.EntryDate BETWEEN @WeekStart AND @WeekEnd
                      AND g.GroupType = 'Purchase'
                      AND g.IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(weekQuery, con))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", weekStart);
                    cmd.Parameters.AddWithValue("@WeekEnd", weekEnd);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            weekTrolley = Convert.ToInt32(reader["WeekTrolley"]);
                            weekEntries = Convert.ToInt32(reader["WeekEntries"]);
                        }
                    }
                }

                // Weekly daily data for summary and chart
                string weeklyDataQuery = @"
                    SELECT
                        d.EntryDate,
                        SUM(ISNULL(d.DayTrolley, 0)) AS DayTrolley,
                        SUM(ISNULL(d.NightTrolley, 0)) AS NightTrolley,
                        SUM(ISNULL(d.DayTrolley, 0) + ISNULL(d.NightTrolley, 0)) AS TotalTrolley
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE d.EntryDate BETWEEN @WeekStart AND @WeekEnd
                      AND g.GroupType = 'Purchase'
                      AND g.IsActive = 1
                    GROUP BY d.EntryDate
                    ORDER BY d.EntryDate";

                using (SqlCommand cmd = new SqlCommand(weeklyDataQuery, con))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", weekStart);
                    cmd.Parameters.AddWithValue("@WeekEnd", weekEnd);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(weeklyData);
                    }
                }
            }

            // Send values to Dashboard View
            ViewBag.WeekStart = weekStart.ToString("dd MMM yyyy");
            ViewBag.WeekEnd = weekEnd.ToString("dd MMM yyyy");

            ViewBag.TodayTrolley = todayTrolley;
            ViewBag.TodayEntries = todayEntries;

            ViewBag.WeekTrolley = weekTrolley;
            ViewBag.WeekEntries = weekEntries;

            return View(weeklyData);
        }
    }
}