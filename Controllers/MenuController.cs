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

        // GET: api/menu (Customer Facing - Only Available Items)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetActiveMenu()
        {
            return await _context.MenuItems.Where(m => m.IsAvailable).ToListAsync();
        }

        // GET: api/menu/all (Admin Facing - All Items)
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetAllMenu()
        {
            return await _context.MenuItems.ToListAsync();
        }

        // POST: api/menu (Admin - Add Item)
        [HttpPost]
        public async Task<ActionResult<MenuItem>> AddMenuItem(MenuItem item)
        {
            _context.MenuItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetActiveMenu), new { id = item.ItemID }, item);
        }

        // PUT: api/menu/5 (Admin - Update Item)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMenuItem(int id, MenuItem item)
        {
            if (id != item.ItemID) return BadRequest();
            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/menu/5 (Admin - Delete Item)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();
            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}