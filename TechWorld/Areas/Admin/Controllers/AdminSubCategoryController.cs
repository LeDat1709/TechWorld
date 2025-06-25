using Microsoft.AspNetCore.Mvc;
using TechWorld.Data;

namespace TechWorld.Areas.Admin.Controllers
{
    public class AdminSubCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AdminSubCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }
    }
}
