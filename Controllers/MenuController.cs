using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Data;
using QRMenuAPI.Models;

namespace QRMenuAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly RestaurantContext _context;

        public MenuController(RestaurantContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetActiveMenu()
        {
            return await _context.MenuItems.Where(m => m.IsAvailable).ToListAsync();
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetAllMenu()
        {
            return await _context.MenuItems.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<MenuItem>> AddMenuItem(MenuItem item)
        {
            _context.MenuItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetActiveMenu), new { id = item.ItemID }, item);
        }

        // UPDATE: Change "int id" to "string id"
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMenuItem(string id, MenuItem item)
        {
            if (id != item.ItemID) return BadRequest();
            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // UPDATE: Change "int id" to "string id"
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMenuItem(string id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();
            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}