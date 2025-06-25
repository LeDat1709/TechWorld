using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminUserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminUserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AdminUser
        public IActionResult Index(string searchString, string role)
        {
            var users = _context.Users.Include(u => u.Rank).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                users = users.Where(u => u.UserName.Contains(searchString) || u.FullName.Contains(searchString) || u.Email.Contains(searchString));
            if (!string.IsNullOrEmpty(role))
                users = users.Where(u => u.Role == role);

            ViewBag.SearchString = searchString;
            ViewBag.Role = role;
            ViewBag.Roles = new[] { "Customer", "Admin" };

            return View(users.ToList());
        }

        // GET: Admin/AdminUser/Create
        public IActionResult Create()
        {
            ViewBag.Ranks = _context.Ranks.ToList();
            return View(new User());
        }

        // POST: Admin/AdminUser/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.Password = BCrypt.Net.BCrypt.HashPassword(model.Password); // Mã hóa mật khẩu
                _context.Users.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm người dùng thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Ranks = _context.Ranks.ToList();
            return View(model);
        }

        // GET: Admin/AdminUser/Edit/5
        public IActionResult Edit(int id)
        {
            var user = _context.Users.Include(u => u.Rank).FirstOrDefault(u => u.UserID == id);
            if (user == null) return NotFound();
            ViewBag.Ranks = _context.Ranks.ToList();
            return View(user);
        }

        // POST: Admin/AdminUser/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserID == model.UserID);
                if (user == null) return NotFound();

                user.UserName = model.UserName;
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.Phone = model.Phone;
                user.Address = model.Address;
                user.Role = model.Role;
                user.RankID = model.RankID;
                user.Points = model.Points;
                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật người dùng thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Ranks = _context.Ranks.ToList();
            return View(model);
        }

        // POST: Admin/AdminUser/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int UserID)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserID == UserID);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
