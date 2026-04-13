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
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace QRMenuAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly RestaurantContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private static readonly HttpClient _httpClient = new HttpClient();

        public OrdersController(RestaurantContext context, IHubContext<OrderHub> hubContext) 
        { 
            _context = context; 
            _hubContext = hubContext; 
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<Order>>> GetActiveOrders()
        {
            return await _context.Orders
                .Where(o => o.Status == "Received" || o.Status == "Preparing")
                .OrderBy(o => o.OrderTime).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult> PlaceOrder(Order incomingOrder)
        {
            try 
            {
                // Fallback ID generation
                if (string.IsNullOrEmpty(incomingOrder.OrderID))
                    incomingOrder.OrderID = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

                // Ensure every item in the list has a unique ID for EF Core tracking
                foreach (var item in incomingOrder.OrderItems)
                {
                    if (string.IsNullOrEmpty(item.OrderItemID))
                        item.OrderItemID = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                }

                incomingOrder.Status = "Received";
                incomingOrder.OrderTime = DateTime.UtcNow;

                _context.Orders.Add(incomingOrder);
                await _context.SaveChangesAsync();

                // Notify Kitchen via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", incomingOrder);
                
                // Fire Email in background
                if (!string.IsNullOrEmpty(incomingOrder.CustomerEmail))
                {
                    // FIX 1: Fetch the menu items HERE while _context is still alive
                    var itemIds = incomingOrder.OrderItems.Select(i => i.ItemID).ToList();
                    var menuItemsForEmail = await _context.MenuItems.Where(m => itemIds.Contains(m.ItemID)).ToListAsync();

                    // Pass the fetched items directly into the background task
                    _ = Task.Run(() => PrepareAndSendEmail(incomingOrder, menuItemsForEmail));
                }

                return Ok(incomingOrder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 ORDER CRASH: {ex.Message}");
                return StatusCode(500, new { error = "Database failed to save multiple items.", message = ex.Message });
            }
        }

        // FIX 2: Added List<MenuItem> parameter so this method doesn't need to touch the database
        private async Task PrepareAndSendEmail(Order order, List<MenuItem> menuItems)
        {
            try
            {
                decimal total = 0;
                string itemsHtml = "";

                foreach (var oi in order.OrderItems)
                {
                    var food = menuItems.FirstOrDefault(m => m.ItemID == oi.ItemID);
                    if (food != null)
                    {
                        decimal lineTotal = oi.Quantity * food.Price;
                        total += lineTotal;
                        itemsHtml += $"<tr><td style='padding:10px; border-bottom:1px solid #333; color:#fff;'><span style='color:#f59e0b;'>{oi.Quantity}x</span> {food.Name}</td><td style='padding:10px; border-bottom:1px solid #333; color:#ccc; text-align:right;'>${lineTotal:F2}</td></tr>";
                    }
                }

                string shortId = order.OrderID.Length > 6 ? order.OrderID.Substring(order.OrderID.Length - 6).ToUpper() : order.OrderID;

                string html = $@"
                <div style='background-color:#000; padding:40px; font-family:sans-serif; color:#fff;'>
                    <div style='max-width:500px; margin:0 auto; background-color:#1a1a1a; padding:30px; border-radius:15px; border:1px solid #333;'>
                        <div style='text-align:center; color:#22c55e; font-size:40px;'>✓</div>
                        <h2 style='text-align:center;'>Order Confirmed</h2>
                        <div style='background-color:#222; padding:20px; border-radius:10px;'>
                            <p><strong>Order ID:</strong> <span style='color:#f59e0b;'>#{shortId}</span></p>
                            <p><strong>Table:</strong> {order.TableID}</p>
                            <table style='width:100%; border-collapse:collapse;'>{itemsHtml}</table>
                            <h3 style='text-align:right; color:#f59e0b;'>Total: ${total:F2}</h3>
                        </div>
                    </div>
                </div>";

                await SendResendEmail(order.CustomerEmail, "Your Luxe Dining Receipt", html);
            }
            catch (Exception ex) { Console.WriteLine($"⚠️ Email Prep Error: {ex.Message}"); }
        }

        private async Task SendResendEmail(string to, string subject, string html)
        {
            string apiKey = "re_aVqxFnBW_MnKGNGwDBwpJVzCKLEtf5W4j";
            var payload = new { from = "Luxe Dining <onboarding@resend.dev>", to = new[] { to }, subject, html };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            // FIX 3: Capture the response and log it if Resend blocks the email
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ RESEND API ERROR ({response.StatusCode}): {errorResponse}");
            }
            else
            {
                Console.WriteLine("✅ Email sent successfully to Resend!");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] int status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();
            order.Status = status == 1 ? "Preparing" : "Ready";
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync(status == 1 ? "OrderPreparing" : "OrderReady", new { OrderID = order.OrderID, TableID = order.TableID });
            return NoContent();
        }
    }
}