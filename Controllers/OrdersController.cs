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
using System.Net.Http; // Needed for Resend API
using System.Text; // Needed for JSON formatting
using System.Text.Json; // Needed for JSON formatting

namespace QRMenuAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly RestaurantContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        
        // Setup a single HTTP client for the Resend API
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
                if (string.IsNullOrEmpty(incomingOrder.OrderID))
                {
                    incomingOrder.OrderID = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                }

                incomingOrder.Status = "Received";
                _context.Orders.Add(incomingOrder);
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", incomingOrder);
                
                // 📧 FIRE AND FORGET: Triggers the email in the background instantly!
                _ = SendReceiptEmailAsync(incomingOrder); 

                return Ok(incomingOrder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 CRITICAL ORDER CRASH: {ex.Message}");
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

        // ==========================================
        // 📧 RESEND API LOGIC
        // ==========================================
        private async Task SendReceiptEmailAsync(Order order)
        {
            try
            {
                if (string.IsNullOrEmpty(order.CustomerEmail)) return;

                // 1. GET THIS FROM RESEND.COM
                string resendApiKey = "re_UyF8GYoD_Ji59M6db9mKU5wpszxouuwoh"; 
                
                // 2. Resend Sandbox mode requires you to send from this exact email address:
                string fromEmail = "onboarding@resend.dev"; 

                string shortId = order.OrderID.Substring(Math.Max(0, order.OrderID.Length - 6)).ToUpper();
                string htmlBody = $"<h2>Thank you for dining with us, {order.CustomerName}!</h2>" +
                                  $"<p>Order ID: #{shortId}</p>" +
                                  $"<p>Your order has been received by the kitchen and is currently being prepared.</p>";

                var payload = new
                {
                    from = $"Luxe Dining <{fromEmail}>",
                    to = new[] { order.CustomerEmail },
                    subject = "Your Luxe Dining Receipt",
                    html = htmlBody
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                request.Headers.Add("Authorization", $"Bearer {resendApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Receipt successfully sent via Resend to {order.CustomerEmail}");
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️ Resend API Failed: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Email Task Crashed: {ex.Message}");
            }
        }
    }
}