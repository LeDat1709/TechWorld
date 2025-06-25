using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminPaymentMethodController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminPaymentMethodController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminPaymentMethods
        public async Task<IActionResult> Index(string searchString)
        {
            var paymentMethods = _context.PaymentMethods.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                paymentMethods = paymentMethods.Where(pm => pm.MethodName.Contains(searchString));
                ViewBag.SearchString = searchString;
            }

            return View(await paymentMethods.ToListAsync());
        }

        // GET: AdminPaymentMethods/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminPaymentMethods/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MethodName,IsActive")] PaymentMethod paymentMethod)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(paymentMethod);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Phương thức thanh toán đã được tạo thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi tạo phương thức thanh toán: {ex.Message}";
                }
            }
            return View(paymentMethod);
        }

        // GET: AdminPaymentMethods/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethod == null)
            {
                return NotFound();
            }
            return View(paymentMethod);
        }

        // POST: AdminPaymentMethods/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentMethodID,MethodName,IsActive")] PaymentMethod paymentMethod)
        {
            if (id != paymentMethod.PaymentMethodID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(paymentMethod);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Phương thức thanh toán đã được cập nhật thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentMethodExists(paymentMethod.PaymentMethodID))
                    {
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Lỗi đồng bộ dữ liệu. Vui lòng thử lại.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi cập nhật phương thức thanh toán: {ex.Message}";
                }
            }
            return View(paymentMethod);
        }

        // GET: AdminPaymentMethods/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PaymentMethodID == id);

            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(paymentMethod);
        }

        // GET: AdminPaymentMethods/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethod = await _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PaymentMethodID == id);

            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(paymentMethod);
        }

        // POST: AdminPaymentMethods/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var paymentMethod = await _context.PaymentMethods.FindAsync(id);
                if (paymentMethod != null)
                {
                    _context.PaymentMethods.Remove(paymentMethod);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Phương thức thanh toán đã được xóa thành công.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy phương thức thanh toán để xóa.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa phương thức thanh toán: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentMethodExists(int id)
        {
            return _context.PaymentMethods.Any(e => e.PaymentMethodID == id);
        }
    }
}
