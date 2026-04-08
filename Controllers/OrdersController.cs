using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Data;
using QRMenuAPI.Hubs;
using QRMenuAPI.Models;

namespace QRMenuAPI.Controllers
{
    public class EmailItemDto { public string Name { get; set; } = string.Empty; public int Quantity { get; set; } public decimal Price { get; set; } }

    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly RestaurantContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrdersController(RestaurantContext context, IHubContext<OrderHub> hubContext) 
        { 
            _context = context; 
            _hubContext = hubContext; 
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<Order>>> GetActiveOrders()
        {
            // Note: .Include() is removed because MongoDB embeds items automatically!
            return await _context.Orders
                .Where(o => o.Status == "Received" || o.Status == "Preparing")
                .OrderBy(o => o.OrderTime).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult> PlaceOrder(Order incomingOrder)
        {
            try 
            {
                incomingOrder.Status = "Received";
                _context.Orders.Add(incomingOrder);
                
                // 1. Save to MongoDB
                await _context.SaveChangesAsync();

                // 2. Ping the Kitchen Dashboard
                await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", incomingOrder);
                
                // 3. Email Logic (Temporarily disabled to prevent crashes)
                // SendReceiptEmail(incomingOrder); 

                return Ok(incomingOrder);
            }
            catch (Exception ex)
            {
                // This catches the crash and prints it to Render instead of throwing a Network Error!
                Console.WriteLine($"🚨 CRITICAL ORDER CRASH: {ex.Message}");
                if (ex.InnerException != null) 
                {
                    Console.WriteLine($"🚨 INNER EXCEPTION: {ex.InnerException.Message}");
                }
                
                return StatusCode(500, new { error = "Backend crashed!", message = ex.Message });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] int status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (status == 1) { order.Status = "Preparing"; }
            else if (status == 2) { order.Status = "Ready"; }

            await _context.SaveChangesAsync();

            if (status == 1) await _hubContext.Clients.All.SendAsync("OrderPreparing", new { OrderID = order.OrderID });
            else if (status == 2) await _hubContext.Clients.All.SendAsync("OrderReady", new { OrderID = order.OrderID, TableID = order.TableID });

            return NoContent();
        }
    }
}