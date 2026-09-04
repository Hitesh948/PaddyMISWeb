using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class PurchaseController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";

        private static readonly int[] AllowedPageSizes = { 25, 50, 100 };


        // =========================================================
        // PURCHASE LIST
        // =========================================================
        [HttpGet]
        public IActionResult Index(
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 50)
        {
            // -----------------------------------------------------
            // VALIDATE PAGE
            // -----------------------------------------------------
            if (page < 1)
            {
                page = 1;
            }

            // -----------------------------------------------------
            // VALIDATE PAGE SIZE
            // -----------------------------------------------------
            if (!AllowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            search = search?.Trim();

            DataTable dt = new DataTable();

            int totalRecords = 0;
            int totalPages = 1;


            using (SqlConnection con =
                   new SqlConnection(connectionString))
            {
                con.Open();


                // =================================================
                // WHERE CONDITION
                // =================================================
                string whereClause = @"
                    WHERE 1 = 1
                ";


                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClause += @"
                        AND
                        (
                            PO_Supplier_Name LIKE @Search
                            OR Site_Name LIKE @Search
                            OR VechNo LIKE @Search
                            OR CONVERT(NVARCHAR(50), WeightSlipNo) LIKE @Search
                        )
                    ";
                }


                if (fromDate.HasValue)
                {
                    whereClause += @"
                        AND WeightDate >= @FromDate
                    ";
                }


                if (toDate.HasValue)
                {
                    whereClause += @"
                        AND WeightDate < @ToDateExclusive
                    ";
                }


                // =================================================
                // COUNT TOTAL RECORDS
                // =================================================
                string countQuery = @"
                    SELECT COUNT(*)
                    FROM PaddyWeightData
                " + whereClause;


                using (SqlCommand countCmd =
                       new SqlCommand(countQuery, con))
                {
                    AddParameters(
                        countCmd,
                        search,
                        fromDate,
                        toDate
                    );

                    totalRecords =
                        Convert.ToInt32(
                            countCmd.ExecuteScalar()
                        );
                }


                // =================================================
                // CALCULATE TOTAL PAGES
                // =================================================
                if (totalRecords > 0)
                {
                    totalPages =
                        (int)Math.Ceiling(
                            totalRecords / (double)pageSize
                        );
                }


                // =================================================
                // KEEP PAGE INSIDE VALID RANGE
                // =================================================
                if (page > totalPages)
                {
                    page = totalPages;
                }


                int offset =
                    (page - 1) * pageSize;


                // =================================================
                // LOAD ONLY CURRENT PAGE
                // =================================================
                string dataQuery = @"
                    SELECT
                        PO_Supplier_Name,
                        Site_Name,
                        VechNo,
                        WeightDate,
                        WeightSlipNo,
                        GrossWeight,
                        TareWeight,
                        MoisturePer,
                        MoistureDeductionWeight,
                        CalculateOnMoisturePer,
                        ExtraDeduction,
                        NetWeight,
                        NetWeightFinal
                    FROM PaddyWeightData
                " + whereClause + @"
                    ORDER BY
                        WeightDate DESC,
                        WeightSlipNo DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;
                ";


                using (SqlCommand dataCmd =
                       new SqlCommand(dataQuery, con))
                {
                    AddParameters(
                        dataCmd,
                        search,
                        fromDate,
                        toDate
                    );


                    dataCmd.Parameters.Add(
                        "@Offset",
                        SqlDbType.Int
                    ).Value = offset;


                    dataCmd.Parameters.Add(
                        "@PageSize",
                        SqlDbType.Int
                    ).Value = pageSize;


                    using (SqlDataAdapter da =
                           new SqlDataAdapter(dataCmd))
                    {
                        da.Fill(dt);
                    }
                }
            }


            // =====================================================
            // SEND VALUES TO VIEW
            // =====================================================
            ViewBag.Search =
                search ?? "";


            ViewBag.FromDate =
                fromDate?.ToString("yyyy-MM-dd") ?? "";


            ViewBag.ToDate =
                toDate?.ToString("yyyy-MM-dd") ?? "";


            ViewBag.CurrentPage =
                page;


            ViewBag.PageSize =
                pageSize;


            ViewBag.TotalRecords =
                totalRecords;


            ViewBag.TotalPages =
                totalPages;


            ViewBag.AllowedPageSizes =
                AllowedPageSizes;


            return View(dt);
        }


        // =========================================================
        // ADD SQL PARAMETERS
        // =========================================================
        private void AddParameters(
            SqlCommand cmd,
            string? search,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                cmd.Parameters.Add(
                    "@Search",
                    SqlDbType.NVarChar,
                    500
                ).Value =
                    "%" + search + "%";
            }


            if (fromDate.HasValue)
            {
                cmd.Parameters.Add(
                    "@FromDate",
                    SqlDbType.DateTime
                ).Value =
                    fromDate.Value.Date;
            }


            if (toDate.HasValue)
            {
                cmd.Parameters.Add(
                    "@ToDateExclusive",
                    SqlDbType.DateTime
                ).Value =
                    toDate.Value.Date.AddDays(1);
            }
        }
    }
}