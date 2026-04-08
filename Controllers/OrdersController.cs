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
using System.Net; // Needed for Email
using System.Net.Mail; // Needed for Email

namespace QRMenuAPI.Controllers
{
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
                
                // Fire the email securely!
                SendReceiptEmail(incomingOrder); 

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
        // 📧 SAFE EMAIL LOGIC
        // ==========================================
        private void SendReceiptEmail(Order order)
        {
            try
            {
                if (string.IsNullOrEmpty(order.CustomerEmail)) return; 

                // CHANGE THESE TWO LINES:
                var fromAddress = new MailAddress("jabeen9945425979@gmail.com", "Luxe Dining");
                const string fromPassword = "zsmlfcxuiovvhnbc"; 

                var toAddress = new MailAddress(order.CustomerEmail);
                const string subject = "Your Luxe Dining Receipt";

                string shortId = order.OrderID.Substring(Math.Max(0, order.OrderID.Length - 6)).ToUpper();
                string body = $"<h2>Thank you for dining with us, {order.CustomerName}!</h2>";
                body += $"<p>Order ID: #{shortId}</p>";
                body += $"<p>Your order has been received by the kitchen and is currently being prepared.</p>";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject, Body = body, IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                }
                Console.WriteLine($"✅ Receipt successfully emailed to {order.CustomerEmail}");
            }
            catch (Exception ex)
            {
                // If the email fails, it logs the error but DOES NOT crash the customer's order!
                Console.WriteLine($"⚠️ Email Failed: {ex.Message}");
            }
        }
    }
}