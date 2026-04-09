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
                // 💎 GENERATE THE LUXE HTML RECEIPT
                // =================================================================
                if (!string.IsNullOrEmpty(incomingOrder.CustomerEmail))
                {
                    // 1. Fetch the real food names and prices from the database
                    var itemIds = incomingOrder.OrderItems.Select(i => i.ItemID).ToList();
                    var menuItems = await _context.MenuItems.Where(m => itemIds.Contains(m.ItemID)).ToListAsync();

                    decimal totalBill = 0;
                    string itemsHtml = "";

                    // 2. Build the receipt rows
                    foreach (var oi in incomingOrder.OrderItems)
                    {
                        var foodItem = menuItems.FirstOrDefault(m => m.ItemID == oi.ItemID);
                        if (foodItem != null)
                        {
                            decimal lineTotal = oi.Quantity * foodItem.Price;
                            totalBill += lineTotal;
                            
                            itemsHtml += $@"
                            <tr>
                                <td style='padding: 10px 0; color: #d4d4d8; border-bottom: 1px solid #27272a;'>
                                    <span style='color: #f59e0b; font-weight: bold; margin-right: 8px;'>{oi.Quantity}x</span> {foodItem.Name}
                                </td>
                                <td style='padding: 10px 0; text-align: right; color: #d4d4d8; border-bottom: 1px solid #27272a;'>
                                    ${lineTotal.ToString("0.00")}
                                </td>
                            </tr>";
                        }
                    }

                    string shortId = incomingOrder.OrderID.Substring(Math.Max(0, incomingOrder.OrderID.Length - 6)).ToUpper();

                    // 3. The Dark-Theme HTML Template
                    string htmlBody = $@"
                    <div style='font-family: ""Helvetica Neue"", Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #09090b; color: #ffffff; padding: 40px; border-radius: 16px; border: 1px solid #27272a;'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h1 style='color: #f59e0b; margin: 0; font-size: 28px; letter-spacing: 2px;'>LUXE DINING</h1>
                            <p style='color: #a1a1aa; font-size: 12px; text-transform: uppercase; letter-spacing: 3px; margin-top: 5px;'>Digital Receipt</p>
                        </div>

                        <p style='font-size: 16px; color: #e4e4e7;'>Hello <strong>{incomingOrder.CustomerName}</strong>,</p>
                        <p style='color: #a1a1aa; line-height: 1.6; margin-bottom: 30px;'>Your order has been sent to the kitchen. We hope you enjoy your meal!</p>

                        <div style='background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; padding: 25px;'>
                            <div style='display: flex; justify-content: space-between; margin-bottom: 20px;'>
                                <p style='margin: 0; color: #a1a1aa; font-size: 14px;'>Order <strong style='color: #ffffff;'>#{shortId}</strong></p>
                                <p style='margin: 0; color: #f59e0b; font-weight: bold; font-size: 14px;'>Table {incomingOrder.TableID}</p>
                            </div>
                            
                            <table style='width: 100%; border-collapse: collapse;'>
                                {itemsHtml}
                            </table>
                            
                            <table style='width: 100%; margin-top: 15px;'>
                                <tr>
                                    <td style='text-align: left; font-size: 14px; color: #a1a1aa; padding-top: 10px;'>Total Amount</td>
                                    <td style='text-align: right; font-weight: bold; font-size: 24px; color: #f59e0b; padding-top: 10px;'>${totalBill.ToString("0.00")}</td>
                                </tr>
                            </table>
                        </div>

                        <p style='text-align: center; color: #71717a; font-size: 12px; margin-top: 40px;'>
                            © {DateTime.Now.Year} Luxe Dining. All rights reserved.
                        </p>
                    </div>";

                    // 4. Fire the email in the background!
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
                string resendApiKey = "re_UyF8GYoD_Ji59M6db9mKU5wpszxouuwoh"; // <--- PUT YOUR RESEND API KEY HERE
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