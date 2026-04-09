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
                if (string.IsNullOrEmpty(incomingOrder.OrderID))
                {
                    incomingOrder.OrderID = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                }

                incomingOrder.Status = "Received";
                _context.Orders.Add(incomingOrder);
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", incomingOrder);
                
                // =================================================================
                // 💎 THE LUXE "SUCCESS MODAL" EMAIL TEMPLATE
                // =================================================================
                if (!string.IsNullOrEmpty(incomingOrder.CustomerEmail))
                {
                    var itemIds = incomingOrder.OrderItems.Select(i => i.ItemID).ToList();
                    var menuItems = await _context.MenuItems.Where(m => itemIds.Contains(m.ItemID)).ToListAsync();

                    decimal totalBill = 0;
                    string itemsHtml = "";

                    foreach (var oi in incomingOrder.OrderItems)
                    {
                        var foodItem = menuItems.FirstOrDefault(m => m.ItemID == oi.ItemID);
                        if (foodItem != null)
                        {
                            decimal lineTotal = oi.Quantity * foodItem.Price;
                            totalBill += lineTotal;
                            
                            itemsHtml += $@"
                            <tr>
                                <td style='padding: 12px 0; color: #e4e4e7; font-size: 15px; border-bottom: 1px solid #3f3f46;'>
                                    <span style='color: #f59e0b; font-weight: bold; margin-right: 10px; background-color: rgba(245, 158, 11, 0.1); padding: 2px 6px; border-radius: 4px;'>{oi.Quantity}x</span> {foodItem.Name}
                                </td>
                                <td style='padding: 12px 0; text-align: right; color: #a1a1aa; font-size: 15px; border-bottom: 1px solid #3f3f46;'>
                                    ${lineTotal.ToString("0.00")}
                                </td>
                            </tr>";
                        }
                    }

                    string shortId = incomingOrder.OrderID.Substring(Math.Max(0, incomingOrder.OrderID.Length - 6)).ToUpper();

                    // HTML STRUCTURE MATCHING THE FRONTEND MODAL
                    string htmlBody = $@"
                    <div style='background-color: #000000; padding: 40px 20px; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color: #ffffff;'>
                        <div style='max-width: 500px; margin: 0 auto; background-color: #1A1A1A; border: 1px solid #27272a; border-radius: 16px; overflow: hidden; box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);'>
                            
                            <div style='text-align: center; padding: 40px 30px 10px;'>
                                <div style='width: 72px; height: 72px; background-color: rgba(34, 197, 94, 0.1); border-radius: 50%; margin: 0 auto 20px; display: table;'>
                                    <div style='display: table-cell; vertical-align: middle;'>
                                        <div style='width: 48px; height: 48px; background-color: #22c55e; border-radius: 50%; margin: 0 auto; display: table;'>
                                            <div style='display: table-cell; vertical-align: middle; color: white; font-size: 24px; font-weight: bold;'>✓</div>
                                        </div>
                                    </div>
                                </div>
                                <h1 style='color: #ffffff; font-size: 24px; margin: 0 0 8px;'>Success</h1>
                                <p style='color: #a1a1aa; font-size: 14px; margin: 0;'>Your order has been placed successfully, {incomingOrder.CustomerName}.</p>
                            </div>

                            <div style='padding: 30px;'>
                                <div style='background-color: #242424; border: 1px solid #3f3f46; border-radius: 12px; padding: 25px;'>
                                    <h2 style='color: #ffffff; margin: 0 0 15px 0; font-size: 16px; border-bottom: 1px solid #3f3f46; padding-bottom: 10px;'>Order Details</h2>
                                    
                                    <table style='width: 100%; margin-bottom: 20px;'>
                                        <tr>
                                            <td style='color: #a1a1aa; font-size: 14px; padding: 4px 0;'>Order ID:</td>
                                            <td style='text-align: right; color: #f59e0b; font-family: monospace; font-size: 16px; font-weight: bold;'>#{shortId}</td>
                                        </tr>
                                        <tr>
                                            <td style='color: #a1a1aa; font-size: 14px; padding: 4px 0;'>Table Number:</td>
                                            <td style='text-align: right; color: #ffffff; font-size: 16px; font-weight: bold;'>{incomingOrder.TableID}</td>
                                        </tr>
                                    </table>

                                    <table style='width: 100%; border-collapse: collapse; margin-bottom: 15px;'>
                                        {itemsHtml}
                                    </table>

                                    <table style='width: 100%;'>
                                        <tr>
                                            <td style='color: #a1a1aa; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; padding-top: 10px;'>Total Bill</td>
                                            <td style='text-align: right; color: #ffffff; font-size: 24px; font-weight: 300; padding-top: 10px;'>${totalBill.ToString("0.00")}</td>
                                        </tr>
                                    </table>
                                </div>
                            </div>

                            <div style='padding: 0 30px 40px; text-align: center;'>
                                <p style='color: #71717a; font-size: 12px; margin: 0;'>
                                    This is an automated receipt from Luxe Dining.<br/>
                                    Please keep this for your records.
                                </p>
                            </div>

                        </div>
                    </div>";

                    _ = SendReceiptEmailAsync(incomingOrder.CustomerEmail, "Your Luxe Dining Receipt", htmlBody);
                }

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
        private async Task SendReceiptEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                string resendApiKey = "re_UyF8GYoD_Ji59M6db9mKU5wpszxouuwoh"; // <--- REPLACE THIS
                string fromEmail = "onboarding@resend.dev"; 

                var payload = new
                {
                    from = $"Luxe Dining <{fromEmail}>",
                    to = new[] { toEmail },
                    subject = subject,
                    html = htmlBody
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                request.Headers.Add("Authorization", $"Bearer {resendApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
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