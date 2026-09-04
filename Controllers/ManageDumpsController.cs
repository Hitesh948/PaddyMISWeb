using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class ManageDumpsController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";


        // =========================================================
        // MAIN PAGE
        // =========================================================

        [HttpGet]
        public IActionResult Index(
            string? search,
            int? unitID,
            string? groupType,
            string? status)
        {
            DataTable dumps = new DataTable();

            search = search?.Trim();

            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT
                        g.GroupID,
                        g.UnitID,
                        u.UnitName,
                        g.GroupName,
                        g.GroupType,
                        g.IsActive
                    FROM Groups g
                    LEFT JOIN Units u
                        ON g.UnitID = u.UnitID
                    WHERE 1 = 1
                ";

                // -------------------------------------------------
                // SEARCH
                // -------------------------------------------------

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += @"
                        AND
                        (
                            g.GroupName LIKE @Search
                            OR u.UnitName LIKE @Search
                        )
                    ";
                }


                // -------------------------------------------------
                // UNIT FILTER
                // -------------------------------------------------

                if (unitID.HasValue && unitID.Value > 0)
                {
                    query += @"
                        AND g.UnitID = @UnitID
                    ";
                }


                // -------------------------------------------------
                // TYPE FILTER
                // -------------------------------------------------

                if (groupType == "Purchase" ||
                    groupType == "Dispatch")
                {
                    query += @"
                        AND g.GroupType = @GroupType
                    ";
                }


                // -------------------------------------------------
                // STATUS FILTER
                // -------------------------------------------------

                if (status == "Active")
                {
                    query += @"
                        AND g.IsActive = 1
                    ";
                }
                else if (status == "Inactive")
                {
                    query += @"
                        AND g.IsActive = 0
                    ";
                }


                query += @"
                    ORDER BY
                        g.GroupType,
                        u.UnitName,
                        g.GroupName;
                ";


                using SqlCommand command =
                    new SqlCommand(
                        query,
                        connection);


                if (!string.IsNullOrWhiteSpace(search))
                {
                    command.Parameters.Add(
                        "@Search",
                        SqlDbType.NVarChar,
                        500).Value =
                        "%" + search + "%";
                }


                if (unitID.HasValue && unitID.Value > 0)
                {
                    command.Parameters.Add(
                        "@UnitID",
                        SqlDbType.Int).Value =
                        unitID.Value;
                }


                if (groupType == "Purchase" ||
                    groupType == "Dispatch")
                {
                    command.Parameters.Add(
                        "@GroupType",
                        SqlDbType.NVarChar,
                        50).Value =
                        groupType;
                }


                using SqlDataAdapter adapter =
                    new SqlDataAdapter(command);

                adapter.Fill(dumps);
            }


            // -----------------------------------------------------
            // LOAD UNITS FOR FILTER
            // -----------------------------------------------------

            DataTable units = new DataTable();

            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                connection.Open();

                string unitQuery = @"
                    SELECT
                        UnitID,
                        UnitName
                    FROM Units
                    WHERE IsActive = 1
                    ORDER BY UnitName;
                ";

                using SqlCommand command =
                    new SqlCommand(
                        unitQuery,
                        connection);

                using SqlDataAdapter adapter =
                    new SqlDataAdapter(command);

                adapter.Fill(units);
            }


            // -----------------------------------------------------
            // SUMMARY COUNTS
            // -----------------------------------------------------

            int totalDumps = 0;
            int activeDumps = 0;
            int inactiveDumps = 0;
            int purchaseDumps = 0;
            int dispatchDumps = 0;


            foreach (DataRow row in dumps.Rows)
            {
                totalDumps++;

                bool isActive =
                    row["IsActive"] != DBNull.Value &&
                    Convert.ToBoolean(row["IsActive"]);

                string type =
                    row["GroupType"]?.ToString() ?? "";


                if (isActive)
                {
                    activeDumps++;
                }
                else
                {
                    inactiveDumps++;
                }


                if (type == "Purchase")
                {
                    purchaseDumps++;
                }
                else if (type == "Dispatch")
                {
                    dispatchDumps++;
                }
            }


            ViewBag.Search =
                search ?? "";

            ViewBag.UnitID =
                unitID?.ToString() ?? "";

            ViewBag.GroupType =
                groupType ?? "";

            ViewBag.Status =
                status ?? "";

            ViewBag.Units =
                units;

            ViewBag.TotalDumps =
                totalDumps;

            ViewBag.ActiveDumps =
                activeDumps;

            ViewBag.InactiveDumps =
                inactiveDumps;

            ViewBag.PurchaseDumps =
                purchaseDumps;

            ViewBag.DispatchDumps =
                dispatchDumps;


            return View(dumps);
        }


        // =========================================================
        // ADD DUMP
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(
            string groupName,
            string groupType,
            int unitID)
        {
            groupName = groupName?.Trim() ?? "";
            groupType = groupType?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(groupName))
            {
                TempData["Error"] =
                    "Dump name is required.";

                return RedirectToAction("Index");
            }

            if (groupType != "Purchase" &&
                groupType != "Dispatch")
            {
                TempData["Error"] =
                    "Invalid dump type.";

                return RedirectToAction("Index");
            }

            if (unitID <= 0)
            {
                TempData["Error"] =
                    "Please select a unit.";

                return RedirectToAction("Index");
            }


            try
            {
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                connection.Open();


                string checkQuery = @"
                    SELECT COUNT(*)
                    FROM Groups
                    WHERE GroupName = @GroupName
                      AND GroupType = @GroupType
                      AND UnitID = @UnitID;
                ";


                using (SqlCommand checkCommand =
                       new SqlCommand(
                           checkQuery,
                           connection))
                {
                    checkCommand.Parameters.Add(
                        "@GroupName",
                        SqlDbType.NVarChar,
                        255).Value =
                        groupName;

                    checkCommand.Parameters.Add(
                        "@GroupType",
                        SqlDbType.NVarChar,
                        50).Value =
                        groupType;

                    checkCommand.Parameters.Add(
                        "@UnitID",
                        SqlDbType.Int).Value =
                        unitID;


                    int count =
                        Convert.ToInt32(
                            checkCommand.ExecuteScalar());


                    if (count > 0)
                    {
                        TempData["Error"] =
                            "This dump already exists for the selected unit and type.";

                        return RedirectToAction("Index");
                    }
                }


                string insertQuery = @"
                    INSERT INTO Groups
                    (
                        UnitID,
                        GroupName,
                        GroupType,
                        IsActive
                    )
                    VALUES
                    (
                        @UnitID,
                        @GroupName,
                        @GroupType,
                        1
                    );
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           insertQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@UnitID",
                        SqlDbType.Int).Value =
                        unitID;

                    command.Parameters.Add(
                        "@GroupName",
                        SqlDbType.NVarChar,
                        255).Value =
                        groupName;

                    command.Parameters.Add(
                        "@GroupType",
                        SqlDbType.NVarChar,
                        50).Value =
                        groupType;

                    command.ExecuteNonQuery();
                }


                TempData["Success"] =
                    "Dump added successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to add dump. " +
                    ex.Message;
            }


            return RedirectToAction("Index");
        }


        // =========================================================
        // EDIT DUMP
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            int groupID,
            string groupName,
            string groupType,
            int unitID,
            bool isActive)
        {
            groupName = groupName?.Trim() ?? "";
            groupType = groupType?.Trim() ?? "";


            if (groupID <= 0)
            {
                TempData["Error"] =
                    "Invalid dump.";

                return RedirectToAction("Index");
            }


            if (string.IsNullOrWhiteSpace(groupName))
            {
                TempData["Error"] =
                    "Dump name is required.";

                return RedirectToAction("Index");
            }


            if (groupType != "Purchase" &&
                groupType != "Dispatch")
            {
                TempData["Error"] =
                    "Invalid dump type.";

                return RedirectToAction("Index");
            }


            if (unitID <= 0)
            {
                TempData["Error"] =
                    "Please select a unit.";

                return RedirectToAction("Index");
            }


            try
            {
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                connection.Open();


                string checkQuery = @"
                    SELECT COUNT(*)
                    FROM Groups
                    WHERE GroupName = @GroupName
                      AND GroupType = @GroupType
                      AND UnitID = @UnitID
                      AND GroupID <> @GroupID;
                ";


                using (SqlCommand checkCommand =
                       new SqlCommand(
                           checkQuery,
                           connection))
                {
                    checkCommand.Parameters.Add(
                        "@GroupName",
                        SqlDbType.NVarChar,
                        255).Value =
                        groupName;

                    checkCommand.Parameters.Add(
                        "@GroupType",
                        SqlDbType.NVarChar,
                        50).Value =
                        groupType;

                    checkCommand.Parameters.Add(
                        "@UnitID",
                        SqlDbType.Int).Value =
                        unitID;

                    checkCommand.Parameters.Add(
                        "@GroupID",
                        SqlDbType.Int).Value =
                        groupID;


                    int count =
                        Convert.ToInt32(
                            checkCommand.ExecuteScalar());


                    if (count > 0)
                    {
                        TempData["Error"] =
                            "Another dump with the same name, unit and type already exists.";

                        return RedirectToAction("Index");
                    }
                }


                string updateQuery = @"
                    UPDATE Groups
                    SET
                        UnitID = @UnitID,
                        GroupName = @GroupName,
                        GroupType = @GroupType,
                        IsActive = @IsActive
                    WHERE GroupID = @GroupID;
                ";


                using (SqlCommand command =
                       new SqlCommand(
                           updateQuery,
                           connection))
                {
                    command.Parameters.Add(
                        "@UnitID",
                        SqlDbType.Int).Value =
                        unitID;

                    command.Parameters.Add(
                        "@GroupName",
                        SqlDbType.NVarChar,
                        255).Value =
                        groupName;

                    command.Parameters.Add(
                        "@GroupType",
                        SqlDbType.NVarChar,
                        50).Value =
                        groupType;

                    command.Parameters.Add(
                        "@IsActive",
                        SqlDbType.Bit).Value =
                        isActive;

                    command.Parameters.Add(
                        "@GroupID",
                        SqlDbType.Int).Value =
                        groupID;

                    command.ExecuteNonQuery();
                }


                TempData["Success"] =
                    "Dump updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to update dump. " +
                    ex.Message;
            }


            return RedirectToAction("Index");
        }


        // =========================================================
        // ACTIVATE / DEACTIVATE
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int groupID)
        {
            if (groupID <= 0)
            {
                TempData["Error"] =
                    "Invalid dump.";

                return RedirectToAction("Index");
            }


            try
            {
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                connection.Open();


                string query = @"
                    UPDATE Groups
                    SET IsActive =
                        CASE
                            WHEN IsActive = 1 THEN 0
                            ELSE 1
                        END
                    WHERE GroupID = @GroupID;
                ";


                using SqlCommand command =
                    new SqlCommand(
                        query,
                        connection);


                command.Parameters.Add(
                    "@GroupID",
                    SqlDbType.Int).Value =
                    groupID;


                command.ExecuteNonQuery();


                TempData["Success"] =
                    "Dump status updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to change dump status. " +
                    ex.Message;
            }


            return RedirectToAction("Index");
        }


        // =========================================================
        // GET ACTIVE UNITS
        // =========================================================

        [HttpGet]
        public JsonResult GetUnits()
        {
            List<object> units =
                new List<object>();


            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                SELECT
                    UnitID,
                    UnitName
                FROM Units
                WHERE IsActive = 1
                ORDER BY UnitName;
            ";


            using SqlCommand command =
                new SqlCommand(
                    query,
                    connection);


            connection.Open();


            using SqlDataReader reader =
                command.ExecuteReader();


            while (reader.Read())
            {
                units.Add(
                    new
                    {
                        unitID =
                            Convert.ToInt32(
                                reader["UnitID"]),

                        unitName =
                            reader["UnitName"]?.ToString()
                            ?? ""
                    });
            }


            return Json(units);
        }
    }
}