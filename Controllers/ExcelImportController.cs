using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using System.Data;

namespace PaddyMISWeb.Controllers
{
    public class ExcelImportController : Controller
    {
        private readonly string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=PaddyTrolleyDB;Trusted_Connection=True;TrustServerCertificate=True;";


        // =========================================================
        // GET: /ExcelImport
        // =========================================================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }


        // =========================================================
        // GET: /ExcelImport/History
        // =========================================================
        [HttpGet]
        public IActionResult History()
        {
            DataTable historyTable = new DataTable();

            using (SqlConnection connection =
                   new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT
                        ImportID,
                        FileName,
                        ImportDate,
                        TotalRows,
                        ImportedRows,
                        SkippedRows
                    FROM ExcelImportHistory
                    ORDER BY ImportDate DESC, ImportID DESC;
                ";

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    using (SqlDataAdapter adapter =
                           new SqlDataAdapter(command))
                    {
                        adapter.Fill(historyTable);
                    }
                }
            }

            return View(historyTable);
        }


        // =========================================================
        // POST: /ExcelImport/Import
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> Import(IFormFile excelFile)
        {
            // -----------------------------------------------------
            // FILE VALIDATION
            // -----------------------------------------------------

            if (excelFile == null || excelFile.Length == 0)
            {
                ViewBag.Error = "Please select an Excel file.";
                return View("Index");
            }


            string extension =
                Path.GetExtension(excelFile.FileName)
                    .ToLowerInvariant();


            if (extension != ".xlsx")
            {
                ViewBag.Error =
                    "Please select an Excel .xlsx file.";

                return View("Index");
            }


            int totalRows = 0;
            int importedRows = 0;
            int skippedRows = 0;

            List<string> errors =
                new List<string>();


            try
            {
                // =================================================
                // OPEN EXCEL
                // =================================================

                using MemoryStream stream =
                    new MemoryStream();

                await excelFile.CopyToAsync(stream);

                stream.Position = 0;


                using XLWorkbook workbook =
                    new XLWorkbook(stream);


                var worksheet =
                    workbook.Worksheet(1);


                var firstRow =
                    worksheet.FirstRowUsed();


                if (firstRow == null)
                {
                    ViewBag.Error =
                        "The Excel file is empty.";

                    return View("Index");
                }


                // =================================================
                // READ EXCEL HEADERS
                // =================================================

                Dictionary<string, int> columns =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);


                foreach (var cell in firstRow.CellsUsed())
                {
                    string header =
                        cell.GetString().Trim();

                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        columns[header] =
                            cell.Address.ColumnNumber;
                    }
                }


                // =================================================
                // REQUIRED COLUMNS
                // =================================================

                string[] requiredColumns =
                {
                    "PO_Supplier_Name",
                    "Site_Name",
                    "VechNo",
                    "WeightDate",
                    "WeightSlipNo",
                    "GrossWeight",
                    "TareWeight",
                    "MoisturePer",
                    "MoistureDeductionWeight",
                    "CalculateOnMoisturePer",
                    "ExtraDeduction",
                    "NetWeight",
                    "NetWeightFinal"
                };


                foreach (string column in requiredColumns)
                {
                    if (!columns.ContainsKey(column))
                    {
                        ViewBag.Error =
                            $"Required column '{column}' was not found in the Excel file.";

                        return View("Index");
                    }
                }


                // =================================================
                // DATABASE
                // =================================================

                using SqlConnection connection =
                    new SqlConnection(connectionString);

                await connection.OpenAsync();


                // =================================================
                // LOAD EXISTING DUPLICATES ONCE
                // =================================================

                HashSet<string> existingKeys =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);


                string existingQuery = @"
                    SELECT
                        WeightSlipNo,
                        VechNo
                    FROM PaddyWeightData
                    WHERE WeightSlipNo IS NOT NULL
                      AND VechNo IS NOT NULL;
                ";


                using (SqlCommand existingCommand =
                       new SqlCommand(
                           existingQuery,
                           connection))
                {
                    using SqlDataReader reader =
                        await existingCommand.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        double slip =
                            Convert.ToDouble(
                                reader["WeightSlipNo"]);

                        string vehicle =
                            reader["VechNo"]?.ToString()?.Trim()
                            ?? "";

                        string key =
                            CreateDuplicateKey(
                                slip,
                                vehicle);

                        existingKeys.Add(key);
                    }
                }


                // =================================================
                // INSERT COMMAND
                // =================================================

                string insertQuery = @"
                    INSERT INTO PaddyWeightData
                    (
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
                    )
                    VALUES
                    (
                        @Supplier,
                        @Site,
                        @Vehicle,
                        @WeightDate,
                        @WeightSlipNo,
                        @GrossWeight,
                        @TareWeight,
                        @MoisturePer,
                        @MoistureDeductionWeight,
                        @CalculateOnMoisturePer,
                        @ExtraDeduction,
                        @NetWeight,
                        @NetWeightFinal
                    );
                ";


                using SqlCommand command =
                    new SqlCommand(
                        insertQuery,
                        connection);


                // =================================================
                // CREATE PARAMETERS ONCE
                // =================================================

                command.Parameters.Add(
                    "@Supplier",
                    SqlDbType.NVarChar,
                    -1);

                command.Parameters.Add(
                    "@Site",
                    SqlDbType.NVarChar,
                    -1);

                command.Parameters.Add(
                    "@Vehicle",
                    SqlDbType.NVarChar,
                    -1);

                command.Parameters.Add(
                    "@WeightDate",
                    SqlDbType.DateTime);

                command.Parameters.Add(
                    "@WeightSlipNo",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@GrossWeight",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@TareWeight",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@MoisturePer",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@MoistureDeductionWeight",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@CalculateOnMoisturePer",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@ExtraDeduction",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@NetWeight",
                    SqlDbType.Float);

                command.Parameters.Add(
                    "@NetWeightFinal",
                    SqlDbType.Float);


                command.Prepare();


                // =================================================
                // TRANSACTION
                // =================================================

                using SqlTransaction transaction =
                    connection.BeginTransaction();

                command.Transaction = transaction;


                try
                {
                    // =================================================
                    // READ EXCEL ROWS
                    // =================================================

                    foreach (
                        var row in worksheet.RowsUsed().Skip(1))
                    {
                        totalRows++;

                        try
                        {
                            // -----------------------------------------
                            // TEXT VALUES
                            // -----------------------------------------

                            string supplier =
                                row.Cell(
                                    columns["PO_Supplier_Name"])
                                .GetString()
                                .Trim();


                            string site =
                                row.Cell(
                                    columns["Site_Name"])
                                .GetString()
                                .Trim();


                            string vehicle =
                                row.Cell(
                                    columns["VechNo"])
                                .GetString()
                                .Trim();


                            // -----------------------------------------
                            // DATE
                            // -----------------------------------------

                            DateTime? weightDate = null;

                            var weightDateCell =
                                row.Cell(
                                    columns["WeightDate"]);


                            if (!weightDateCell.IsEmpty())
                            {
                                if (
                                    weightDateCell
                                        .TryGetValue<DateTime>(
                                            out DateTime dateValue))
                                {
                                    weightDate =
                                        dateValue;
                                }
                                else if (
                                    DateTime.TryParse(
                                        weightDateCell.GetString(),
                                        out DateTime parsedDate))
                                {
                                    weightDate =
                                        parsedDate;
                                }
                            }


                            // -----------------------------------------
                            // WEIGHT SLIP
                            // -----------------------------------------

                            double? slipNo = null;

                            var slipCell =
                                row.Cell(
                                    columns["WeightSlipNo"]);


                            if (!slipCell.IsEmpty())
                            {
                                if (
                                    slipCell
                                        .TryGetValue<double>(
                                            out double slipValue))
                                {
                                    slipNo =
                                        slipValue;
                                }
                                else if (
                                    double.TryParse(
                                        slipCell.GetString(),
                                        out double parsedSlip))
                                {
                                    slipNo =
                                        parsedSlip;
                                }
                            }


                            // -----------------------------------------
                            // NUMERIC VALUES
                            // -----------------------------------------

                            double? grossWeight =
                                GetDouble(
                                    row.Cell(
                                        columns["GrossWeight"]));


                            double? tareWeight =
                                GetDouble(
                                    row.Cell(
                                        columns["TareWeight"]));


                            double? moisturePer =
                                GetDouble(
                                    row.Cell(
                                        columns["MoisturePer"]));


                            double? moistureDeduction =
                                GetDouble(
                                    row.Cell(
                                        columns[
                                            "MoistureDeductionWeight"]));


                            double? calculateMoisture =
                                GetDouble(
                                    row.Cell(
                                        columns[
                                            "CalculateOnMoisturePer"]));


                            double? extraDeduction =
                                GetDouble(
                                    row.Cell(
                                        columns[
                                            "ExtraDeduction"]));


                            double? netWeight =
                                GetDouble(
                                    row.Cell(
                                        columns["NetWeight"]));


                            double? netWeightFinal =
                                GetDouble(
                                    row.Cell(
                                        columns["NetWeightFinal"]));


                            // -----------------------------------------
                            // EMPTY ROW
                            // -----------------------------------------

                            if (
                                string.IsNullOrWhiteSpace(
                                    supplier)
                                &&
                                string.IsNullOrWhiteSpace(
                                    site)
                                &&
                                string.IsNullOrWhiteSpace(
                                    vehicle)
                                &&
                                weightDate == null)
                            {
                                totalRows--;
                                continue;
                            }


                            // =================================================
                            // DUPLICATE CHECK
                            // =================================================

                            if (
                                slipNo.HasValue
                                &&
                                !string.IsNullOrWhiteSpace(
                                    vehicle))
                            {
                                string key =
                                    CreateDuplicateKey(
                                        slipNo.Value,
                                        vehicle);


                                if (existingKeys.Contains(key))
                                {
                                    skippedRows++;
                                    continue;
                                }


                                existingKeys.Add(key);
                            }


                            // =================================================
                            // SET PARAMETERS
                            // =================================================

                            command.Parameters[
                                "@Supplier"].Value =
                                string.IsNullOrWhiteSpace(supplier)
                                ? DBNull.Value
                                : supplier;


                            command.Parameters[
                                "@Site"].Value =
                                string.IsNullOrWhiteSpace(site)
                                ? DBNull.Value
                                : site;


                            command.Parameters[
                                "@Vehicle"].Value =
                                string.IsNullOrWhiteSpace(vehicle)
                                ? DBNull.Value
                                : vehicle;


                            command.Parameters[
                                "@WeightDate"].Value =
                                (object?)weightDate
                                ?? DBNull.Value;


                            command.Parameters[
                                "@WeightSlipNo"].Value =
                                (object?)slipNo
                                ?? DBNull.Value;


                            command.Parameters[
                                "@GrossWeight"].Value =
                                (object?)grossWeight
                                ?? DBNull.Value;


                            command.Parameters[
                                "@TareWeight"].Value =
                                (object?)tareWeight
                                ?? DBNull.Value;


                            command.Parameters[
                                "@MoisturePer"].Value =
                                (object?)moisturePer
                                ?? DBNull.Value;


                            command.Parameters[
                                "@MoistureDeductionWeight"].Value =
                                (object?)moistureDeduction
                                ?? DBNull.Value;


                            command.Parameters[
                                "@CalculateOnMoisturePer"].Value =
                                (object?)calculateMoisture
                                ?? DBNull.Value;


                            command.Parameters[
                                "@ExtraDeduction"].Value =
                                (object?)extraDeduction
                                ?? DBNull.Value;


                            command.Parameters[
                                "@NetWeight"].Value =
                                (object?)netWeight
                                ?? DBNull.Value;


                            command.Parameters[
                                "@NetWeightFinal"].Value =
                                (object?)netWeightFinal
                                ?? DBNull.Value;


                            // =================================================
                            // INSERT
                            // =================================================

                            await command.ExecuteNonQueryAsync();

                            importedRows++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(
                                $"Row {row.RowNumber()}: {ex.Message}");
                        }
                    }


                    // =================================================
                    // COMMIT TRANSACTION
                    // =================================================

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }


                // =================================================
                // SAVE IMPORT HISTORY
                // =================================================

                string historyQuery = @"
                    INSERT INTO ExcelImportHistory
                    (
                        FileName,
                        ImportDate,
                        TotalRows,
                        ImportedRows,
                        SkippedRows
                    )
                    VALUES
                    (
                        @FileName,
                        GETDATE(),
                        @TotalRows,
                        @ImportedRows,
                        @SkippedRows
                    );
                ";


                using SqlCommand historyCommand =
                    new SqlCommand(
                        historyQuery,
                        connection);


                historyCommand.Parameters.Add(
                    "@FileName",
                    SqlDbType.NVarChar,
                    255).Value =
                    excelFile.FileName;


                historyCommand.Parameters.Add(
                    "@TotalRows",
                    SqlDbType.Int).Value =
                    totalRows;


                historyCommand.Parameters.Add(
                    "@ImportedRows",
                    SqlDbType.Int).Value =
                    importedRows;


                historyCommand.Parameters.Add(
                    "@SkippedRows",
                    SqlDbType.Int).Value =
                    skippedRows;


                await historyCommand.ExecuteNonQueryAsync();


                // =================================================
                // RESULT
                // =================================================

                ViewBag.FileName =
                    excelFile.FileName;


                ViewBag.TotalRows =
                    totalRows;


                ViewBag.ImportedRows =
                    importedRows;


                ViewBag.SkippedRows =
                    skippedRows;


                ViewBag.Errors =
                    errors;


                ViewBag.Success =
                    $"Import completed successfully. {importedRows} records imported.";


                return View("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Errors =
                    new List<string>
                    {
                        ex.Message
                    };


                return View("Index");
            }
        }


        // =========================================================
        // CREATE DUPLICATE KEY
        // =========================================================
        private static string CreateDuplicateKey(
            double slipNo,
            string vehicle)
        {
            return
                slipNo.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + "|"
                + vehicle.Trim().ToUpperInvariant();
        }


        // =========================================================
        // GET DOUBLE
        // =========================================================
        private static double? GetDouble(
            IXLCell cell)
        {
            if (cell.IsEmpty())
            {
                return null;
            }


            if (
                cell.TryGetValue<double>(
                    out double value))
            {
                return value;
            }


            if (
                double.TryParse(
                    cell.GetString(),
                    out double parsed))
            {
                return parsed;
            }


            return null;
        }
    }
}