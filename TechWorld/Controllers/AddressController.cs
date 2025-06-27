using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TechWorld.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AddressController> _logger;

        public AddressController(ApplicationDbContext context, ILogger<AddressController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            if (User.Identity.IsAuthenticated && User.FindFirst(ClaimTypes.NameIdentifier) != null)
            {
                return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            }
            throw new UnauthorizedAccessException("User is not authenticated or User ID not found.");
        }

        public async Task<IActionResult> Index(bool selectMode = false)
        {
            var userId = GetCurrentUserId();
            var userAddresses = await _context.UserAddresses
                                            .Where(ua => ua.UserID == userId)
                                            .Include(ua => ua.User)
                                            .Include(ua => ua.Province)
                                            .Include(ua => ua.District)
                                            .Include(ua => ua.Ward)
                                            .OrderByDescending(ua => ua.IsDefault)
                                            .ThenByDescending(ua => ua.UserAddressID)
                                            .ToListAsync();
            ViewBag.SelectMode = selectMode;
            return View(userAddresses);
        }

        public async Task<IActionResult> Create(bool selectMode = false)
        {
            ViewBag.Provinces = await _context.Provinces.OrderBy(p => p.ProvinceName).ToListAsync();
            ViewBag.SelectMode = selectMode;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Address,ProvinceID,DistrictID,WardID,PhoneNumber,IsDefault")] UserAddress userAddress, bool selectMode = false)
        {
            var userId = GetCurrentUserId();
            userAddress.UserID = userId;
            userAddress.AddedAt = DateTime.Now;

            // Remove User from ModelState validation
            ModelState.Remove("User");
            ModelState.Remove("Province");
            ModelState.Remove("District");
            ModelState.Remove("Ward");

            if (ModelState.IsValid)
            {
                try
                {
                    // If this address is set as default, unset all other default addresses
                    if (userAddress.IsDefault)
                    {
                        await UnsetDefaultAddresses(userId);
                    }
                    else
                    {
                        // If this is the first address, make it default
                        var hasExistingAddresses = await _context.UserAddresses
                            .AnyAsync(ua => ua.UserID == userId);
                        if (!hasExistingAddresses)
                        {
                            userAddress.IsDefault = true;
                        }
                    }

                    _context.Add(userAddress);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Địa chỉ đã được thêm thành công!";
                    return RedirectToAction(nameof(Index), new { selectMode = selectMode });
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database error occurred while adding address for user {UserId}: {Message}", userId, dbEx.Message);
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi lưu địa chỉ vào cơ sở dữ liệu. Vui lòng kiểm tra lại thông tin và thử lại.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while adding address for user {UserId}: {Message}", userId, ex.Message);
                    ModelState.AddModelError("", "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau.");
                }
            }

            // Reload dropdowns when there's an error
            await LoadDropdownData(userAddress);
            ViewBag.SelectMode = selectMode;
            return View(userAddress);
        }

        public async Task<IActionResult> Edit(int? id, bool selectMode = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var userAddress = await _context.UserAddresses
                                            .FirstOrDefaultAsync(ua => ua.UserAddressID == id && ua.UserID == userId);

            if (userAddress == null)
            {
                return NotFound();
            }

            await LoadDropdownData(userAddress);
            ViewBag.SelectMode = selectMode;
            return View(userAddress);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserAddressID,Address,ProvinceID,DistrictID,WardID,PhoneNumber,IsDefault")] UserAddress userAddress, bool selectMode = false)
        {
            if (id != userAddress.UserAddressID)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var existingAddress = await _context.UserAddresses.AsNoTracking()
                .FirstOrDefaultAsync(ua => ua.UserAddressID == id);

            if (existingAddress == null || existingAddress.UserID != userId)
            {
                return Forbid();
            }

            userAddress.UserID = userId;
            userAddress.CreatedAt = existingAddress.CreatedAt;
            userAddress.AddedAt = DateTime.Now;

            // Remove navigation properties from ModelState validation
            ModelState.Remove("User");
            ModelState.Remove("Province");
            ModelState.Remove("District");
            ModelState.Remove("Ward");

            if (ModelState.IsValid)
            {
                try
                {
                    if (userAddress.IsDefault)
                    {
                        await UnsetDefaultAddresses(userId, userAddress.UserAddressID);
                    }
                    else if (existingAddress.IsDefault)
                    {
                        // If removing default status, set another address as default
                        var otherAddress = await _context.UserAddresses
                            .Where(ua => ua.UserID == userId && ua.UserAddressID != userAddress.UserAddressID)
                            .FirstOrDefaultAsync();

                        if (otherAddress != null)
                        {
                            otherAddress.IsDefault = true;
                            _context.Update(otherAddress);
                        }
                        else
                        {
                            // If this is the only address, it must remain default
                            userAddress.IsDefault = true;
                        }
                    }

                    _context.Update(userAddress);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Địa chỉ đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Index), new { selectMode = selectMode });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserAddressExists(userAddress.UserAddressID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            await LoadDropdownData(userAddress);
            ViewBag.SelectMode = selectMode;
            return View(userAddress);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool selectMode = false)
        {
            var userId = GetCurrentUserId();
            var userAddress = await _context.UserAddresses
                .FirstOrDefaultAsync(ua => ua.UserAddressID == id && ua.UserID == userId);

            if (userAddress == null)
            {
                return NotFound();
            }

            _context.UserAddresses.Remove(userAddress);
            await _context.SaveChangesAsync();

            // If deleted address was default, set another as default
            if (userAddress.IsDefault)
            {
                var newDefaultAddress = await _context.UserAddresses
                    .Where(ua => ua.UserID == userId)
                    .OrderBy(ua => ua.UserAddressID)
                    .FirstOrDefaultAsync();

                if (newDefaultAddress != null)
                {
                    newDefaultAddress.IsDefault = true;
                    _context.Update(newDefaultAddress);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Địa chỉ đã được xóa thành công!";
            return RedirectToAction(nameof(Index), new { selectMode = selectMode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id, bool selectMode = false)
        {
            var userId = GetCurrentUserId();
            var userAddress = await _context.UserAddresses
                .FirstOrDefaultAsync(ua => ua.UserAddressID == id && ua.UserID == userId);

            if (userAddress == null)
            {
                return NotFound();
            }

            await UnsetDefaultAddresses(userId, id);
            userAddress.IsDefault = true;
            _context.Update(userAddress);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Địa chỉ mặc định đã được cập nhật!";
            return RedirectToAction(nameof(Index), new { selectMode = selectMode });
        }

        private async Task UnsetDefaultAddresses(int userId, int? exceptAddressId = null)
        {
            var defaultAddresses = await _context.UserAddresses
                .Where(ua => ua.UserID == userId && ua.IsDefault && ua.UserAddressID != exceptAddressId)
                .ToListAsync();

            foreach (var addr in defaultAddresses)
            {
                addr.IsDefault = false;
            }

            if (defaultAddresses.Any())
            {
                _context.UserAddresses.UpdateRange(defaultAddresses);
            }
        }

        private async Task LoadDropdownData(UserAddress userAddress)
        {
            ViewBag.Provinces = await _context.Provinces.OrderBy(p => p.ProvinceName).ToListAsync();

            if (userAddress.ProvinceID > 0)
            {
                ViewBag.Districts = await _context.Districts
                    .Where(d => d.ProvinceID == userAddress.ProvinceID)
                    .OrderBy(d => d.DistrictName)
                    .ToListAsync();

                if (userAddress.DistrictID > 0)
                {
                    ViewBag.Wards = await _context.Wards
                        .Where(w => w.DistrictID == userAddress.DistrictID)
                        .OrderBy(w => w.WardName)
                        .ToListAsync();
                }
            }
        }

        private bool UserAddressExists(int id)
        {
            return _context.UserAddresses.Any(e => e.UserAddressID == id);
        }

        [HttpGet]
        public async Task<JsonResult> GetDistricts(int provinceId)
        {
            var districts = await _context.Districts
                .Where(d => d.ProvinceID == provinceId)
                .Select(d => new { d.DistrictID, d.DistrictName })
                .OrderBy(d => d.DistrictName)
                .ToListAsync();
            return Json(districts);
        }

        [HttpGet]
        public async Task<JsonResult> GetWards(int districtId)
        {
            var wards = await _context.Wards
                .Where(w => w.DistrictID == districtId)
                .Select(w => new { w.WardID, w.WardName })
                .OrderBy(w => w.WardName)
                .ToListAsync();
            return Json(wards);
        }

        [HttpGet]
        public async Task<JsonResult> GetAllLocations()
        {
            var provinces = await _context.Provinces
                .Include(p => p.Districts)
                    .ThenInclude(d => d.Wards)
                .Select(p => new
                {
                    p.ProvinceID,
                    p.ProvinceName,
                    Districts = p.Districts.Select(d => new
                    {
                        d.DistrictID,
                        d.DistrictName,
                        Wards = d.Wards.Select(w => new
                        {
                            w.WardID,
                            w.WardName
                        }).ToList()
                    }).ToList()
                })
                .OrderBy(p => p.ProvinceName)
                .ToListAsync();
            return Json(provinces);
        }
    }
}
