using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class DailyReportController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // =========================================================
        // DAILY REPORT
        // =========================================================
        [HttpGet]
        public IActionResult Index(DateTime? reportDate)
        {
            DateTime selectedDate = reportDate?.Date ?? DateTime.Today;

            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        d.EntryID,
                        d.EntryDate,
                        d.GroupID,
                        g.GroupName,
                        d.DayTrolley,
                        d.NightTrolley,
                        ISNULL(d.DayTrolley, 0) + ISNULL(d.NightTrolley, 0) AS TotalTrolley
                    FROM DailyTrolleyDetails d
                    INNER JOIN Groups g
                        ON d.GroupID = g.GroupID
                    WHERE d.EntryDate = @EntryDate
                      AND g.GroupType = 'Purchase'
                    ORDER BY g.GroupName";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@EntryDate", SqlDbType.Date)
                        .Value = selectedDate;

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            ViewBag.ReportDate = selectedDate.ToString("yyyy-MM-dd");

            return View(dt);
        }


        // =========================================================
        // GET ACTIVE PURCHASE DUMPS
        // =========================================================
        [HttpGet]
        public JsonResult GetDumps()
        {
            List<object> dumps = new List<object>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        GroupID,
                        GroupName
                    FROM Groups
                    WHERE GroupType = 'Purchase'
                      AND IsActive = 1
                    ORDER BY GroupName";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dumps.Add(new
                            {
                                groupID = Convert.ToInt32(reader["GroupID"]),
                                groupName = reader["GroupName"].ToString()
                            });
                        }
                    }
                }
            }

            return Json(dumps);
        }


        // =========================================================
        // SAVE / UPDATE DAILY TROLLEY ENTRY
        // =========================================================
        [HttpPost]
        public IActionResult Save(
            DateTime entryDate,
            int groupID,
            int dayTrolley,
            int nightTrolley)
        {
            DateTime selectedDate = entryDate.Date;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                // -------------------------------------------------
                // Check whether this dump already has an entry
                // for the selected date
                // -------------------------------------------------
                string checkQuery = @"
                    SELECT COUNT(*)
                    FROM DailyTrolleyDetails
                    WHERE EntryDate = @EntryDate
                      AND GroupID = @GroupID";

                int existingCount;

                using (SqlCommand checkCmd =
                       new SqlCommand(checkQuery, con))
                {
                    checkCmd.Parameters.Add("@EntryDate", SqlDbType.Date)
                        .Value = selectedDate;

                    checkCmd.Parameters.Add("@GroupID", SqlDbType.Int)
                        .Value = groupID;

                    existingCount =
                        Convert.ToInt32(checkCmd.ExecuteScalar());
                }


                // -------------------------------------------------
                // UPDATE existing entry
                // -------------------------------------------------
                if (existingCount > 0)
                {
                    string updateQuery = @"
                        UPDATE DailyTrolleyDetails
                        SET
                            DayTrolley = @DayTrolley,
                            NightTrolley = @NightTrolley
                        WHERE EntryDate = @EntryDate
                          AND GroupID = @GroupID";

                    using (SqlCommand updateCmd =
                           new SqlCommand(updateQuery, con))
                    {
                        updateCmd.Parameters.Add("@DayTrolley", SqlDbType.Int)
                            .Value = dayTrolley;

                        updateCmd.Parameters.Add("@NightTrolley", SqlDbType.Int)
                            .Value = nightTrolley;

                        updateCmd.Parameters.Add("@EntryDate", SqlDbType.Date)
                            .Value = selectedDate;

                        updateCmd.Parameters.Add("@GroupID", SqlDbType.Int)
                            .Value = groupID;

                        updateCmd.ExecuteNonQuery();
                    }
                }


                // -------------------------------------------------
                // INSERT new entry
                // -------------------------------------------------
                else
                {
                    string insertQuery = @"
                        INSERT INTO DailyTrolleyDetails
                        (
                            GroupID,
                            EntryDate,
                            DayTrolley,
                            NightTrolley
                        )
                        VALUES
                        (
                            @GroupID,
                            @EntryDate,
                            @DayTrolley,
                            @NightTrolley
                        )";

                    using (SqlCommand insertCmd =
                           new SqlCommand(insertQuery, con))
                    {
                        insertCmd.Parameters.Add("@GroupID", SqlDbType.Int)
                            .Value = groupID;

                        insertCmd.Parameters.Add("@EntryDate", SqlDbType.Date)
                            .Value = selectedDate;

                        insertCmd.Parameters.Add("@DayTrolley", SqlDbType.Int)
                            .Value = dayTrolley;

                        insertCmd.Parameters.Add("@NightTrolley", SqlDbType.Int)
                            .Value = nightTrolley;

                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            return RedirectToAction(
                "Index",
                new
                {
                    reportDate = selectedDate.ToString("yyyy-MM-dd")
                });
        }


        // =========================================================
        // DELETE ENTRY
        // =========================================================
        [HttpPost]
        public IActionResult Delete(
            int id,
            DateTime reportDate)
        {
            DateTime selectedDate = reportDate.Date;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    DELETE FROM DailyTrolleyDetails
                    WHERE EntryID = @EntryID";

                using (SqlCommand cmd =
                       new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@EntryID", SqlDbType.Int)
                        .Value = id;

                    con.Open();

                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction(
                "Index",
                new
                {
                    reportDate = selectedDate.ToString("yyyy-MM-dd")
                });
        }
    }
}