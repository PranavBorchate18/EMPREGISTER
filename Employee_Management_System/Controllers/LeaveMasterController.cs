using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class LeaveMasterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveMasterController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.LeaveMasters.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.Name != null && x.Name.Contains(search));
            }

            var data = await query.OrderBy(x => x.Code).ToListAsync();
            ViewBag.Search = search;
            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            short lastCode = await _context.LeaveMasters
                .AsNoTracking()
                .Select(x => (short?)x.Code)
                .MaxAsync() ?? 0;

            if (lastCode == short.MaxValue)
            {
                TempData["ErrorMessage"] =
                    "New Leave Code cannot be generated because the maximum code has been reached.";
                return RedirectToAction(nameof(Index));
            }

            return View(new LeaveMaster
            {
                Code = (short)(lastCode + 1),
                Active = "Y"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveMaster model)
        {
            if (!ModelState.IsValid)
            {
                short currentLastCode = await _context.LeaveMasters
                    .AsNoTracking()
                    .Select(x => (short?)x.Code)
                    .MaxAsync() ?? 0;

                if (currentLastCode < short.MaxValue)
                {
                    model.Code = (short)(currentLastCode + 1);
                    ModelState.Remove(nameof(model.Code));
                }

                return View(model);
            }

            short lastCode = await _context.LeaveMasters
                .Select(x => (short?)x.Code)
                .MaxAsync() ?? 0;

            if (lastCode == short.MaxValue)
            {
                ModelState.AddModelError("",
                    "New Leave Code cannot be generated because the maximum code has been reached.");
                return View(model);
            }

            model.Code = (short)(lastCode + 1);
            model.Active = "Y";

            _context.LeaveMasters.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Leave Type added successfully. Leave Code: {model.Code}";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(short id)
        {
            var leave = await _context.LeaveMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == id);

            if (leave == null)
                return NotFound();

            return View(leave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LeaveMaster model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var leave = await _context.LeaveMasters
                .FirstOrDefaultAsync(x => x.Code == model.Code);

            if (leave == null)
                return NotFound();

            leave.Name = model.Name;
            leave.ShortName = model.ShortName;
            leave.PermanentYear = model.PermanentYear;
            leave.PermanentMonth = model.PermanentMonth;
            leave.TemporaryMonth = model.TemporaryMonth;
            leave.StoreYN = model.StoreYN;
            leave.StoreLimit = model.StoreLimit;
            leave.CashLimit = model.CashLimit;
            leave.Flag = model.Flag;
            leave.MinimumLimit = model.MinimumLimit;
            leave.LeaveLimit = model.LeaveLimit;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Leave Type updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(short id)
        {
            var leave = await _context.LeaveMasters
                .FirstOrDefaultAsync(x => x.Code == id);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave Type not found.";
                return RedirectToAction(nameof(Index));
            }

            leave.Active =
                string.Equals(leave.Active?.Trim(), "Y",
                    StringComparison.OrdinalIgnoreCase)
                    ? "N"
                    : "Y";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{leave.Name ?? "Leave Type"} is now " +
                (leave.Active == "Y" ? "Active." : "Deactive.");

            return RedirectToAction(nameof(Index));
        }
    }
}
