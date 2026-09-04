using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PaddyMISWeb.Data;

namespace PaddyMISWeb.Controllers
{
    public class TestController : Controller
    {
        private readonly DatabaseHelper _database;

        public TestController(DatabaseHelper database)
        {
            _database = database;
        }

        public IActionResult Index()
        {
            try
            {
                using SqlConnection con = _database.GetConnection();

                con.Open();

                ViewBag.Message =
                    "SUCCESS! Connected to PaddyTrolleyDB.";

                ViewBag.Server =
                    con.DataSource;

                ViewBag.Database =
                    con.Database;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Message =
                    "DATABASE CONNECTION FAILED";

                ViewBag.Error =
                    ex.Message;

                return View();
            }
        }
    }
}