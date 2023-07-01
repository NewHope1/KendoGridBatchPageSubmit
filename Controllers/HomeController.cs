using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.Ajax.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Telerik.SvgIcons;

namespace KendoGridBatchPageSubmit.Controllers
{
    public class HomeController : Controller
    {
        static HomeController()
        {
            
        }

        public ActionResult Edit()
        {
            Customer cust = new Customer();
            return View(cust);
        }

        // POST: Default/Edit/5
        [AcceptVerbs("Post")]
        public ActionResult Edit(string lookupBtn, string submitBtn, FormCollection fc, Customer viewModel)
        {
            using (NorthwindEntities _database = new NorthwindEntities())
            {
                ModelState.Clear();

                var customer = _database.Customers.Where(o => o.CustomerID == viewModel.CustomerID).FirstOrDefault();

                if (!String.IsNullOrEmpty(lookupBtn)) // lookup button press, just return customer found (yes, ideallly, I would have handled the Lookup with an Ajax call from the client, but I didn't want to digress from the main topic and the view is simple and efficiency is not a factor here)
                {
                    return View(customer);
                }
                else if (!String.IsNullOrEmpty(submitBtn)) // Page submit (save) press
                {
                    // Save all view fields

                    // Save customer info
                    customer.ContactName = viewModel.ContactName;
                    customer.ContactTitle = viewModel.ContactTitle;
                    customer.Address = viewModel.Address;


                    // Now let's save customer orders...

                    var addedRows = fc["CreatedRowsData"];
                    var updatedRows = fc["UpdatedRowsData"];
                    var deletedRows = fc["DeletedRowsData"];

                    List<Order> addedOrders = getOrders(addedRows);
                    List<Order> updatedOrders = getOrders(updatedRows);
                    List<Order> deletedOrders = getOrders(deletedRows);

                    // process addedRows
                    if (addedOrders != null)
                    {
                        addedOrders.ForEach(o =>
                        {
                            if (!customer.Orders.ToList().Exists(c => c.OrderID == o.OrderID))
                            {
                                var newOrder = _database.Orders.Create();

                                newOrder.OrderDate = o.OrderDate;
                                newOrder.Freight = o.Freight;
                                newOrder.ShipCity = o.ShipCity;
                                newOrder.ShipCountry = o.ShipCountry;
                                newOrder.OrderID = newOrder.OrderID + 1; // to allow adding more than one row so that the next time around in the loop, it doesn't find that this is an existing row

                                customer.Orders.Add(newOrder);
                            }
                        });
                    }

                    // process updatedRows
                    if (updatedOrders != null)
                    {
                        updatedOrders.ForEach(u =>
                            {
                                if (customer.Orders.ToList().Exists(c => c.OrderID == u.OrderID))
                                {
                                    var existingOrder = customer.Orders.AsQueryable().FirstOrDefault(f => f.OrderID == u.OrderID);

                                    existingOrder.OrderDate = u.OrderDate;
                                    existingOrder.Freight = u.Freight;
                                    existingOrder.ShipCity = u.ShipCity;
                                    existingOrder.ShipCountry = u.ShipCountry;
                                }
                            });
                    }

                    // process deletedRows
                    if (deletedOrders != null)
                    {
                        deletedOrders.ForEach(d =>
                        {
                            if (customer.Orders.ToList().Exists(c => c.OrderID == d.OrderID))
                            {
                                // must delete order details first so we don't get a constraint violation when deleting the order
                                var orderDetailsToDelete = _database.Order_Details.Where(t => d.OrderID == d.OrderID);
                                _database.Order_Details.RemoveRange(orderDetailsToDelete);

                                var existingOrder = customer.Orders.AsQueryable().FirstOrDefault(f => f.OrderID == d.OrderID);
                                _database.Entry(existingOrder).State = System.Data.Entity.EntityState.Deleted;
                            }
                        });
                    }

                    _database.SaveChanges();
                }

                return (View(customer));
            }
        }

        /// <summary>
        /// Return a list of Order objects from a string record
        /// </summary>
        /// <param name="record">input string with columns delimeted by comma. For example:
        /// "[{\"OrderID\":10254,\"CustomerID\":null,\"EmployeeID\":null,\"OrderDate\":\"1996-07-11T04:00:00.000Z\",\"RequiredDate\":\"1996-08-08T04:00:00.000Z\",\"ShippedDate\":\"1996-07-23T04:00:00.000Z\",\"ShipVia\":2,\"Freight\":45,\"ShipName\":\"Chop-suey Chinese\",\"ShipAddress\":\"Hauptstr. 31\",\"ShipCity\":\"Bern\",\"ShipRegion\":null,\"ShipPostalCode\":\"3012\",\"ShipCountry\":\"Switzerland\",\"Customer\":null,\"Order_Details\":[]}]"
        /// This parameter comes from the client side
        /// </param>
        /// <returns>List of Order objects</returns>
        private List<Order> getOrders(string record)
        {
            if (record == "[]") // no new rows added to grid
            {
                return null;
            }

            List<Order> rVal = new List<Order>();

            Dictionary<string, string> columns = new Dictionary<string, string>();

            string tempRecord = record;
            ArrayList recs = new ArrayList();
            var matcher = new Regex(@"{(.*?)}"); // match all records in between { and }
            var matches = matcher.Matches(record).Cast<Match>().Select(m => m.Value).Distinct(); // cast to IEnumerable so we can operate on
            foreach (string match in matches)
            {
                tempRecord = match.Replace("{", String.Empty); // remove beginning {
                tempRecord = tempRecord.Replace("}", String.Empty); // remove ending }
                tempRecord = tempRecord.Replace("\"", String.Empty); // remove all double quotes
                recs.Add(tempRecord);
            };

            // loop through all records (rows) and convert each one into an entity
            foreach (String rec in recs)
            {
                var fields = rec.Split(',');

                // loop through the record fields
                foreach (var field in fields)
                {
                    var fieldName = field.Split(':')[0];

                    // Deal with OrderDate and RequiredDate fields case. E.g., OrderDate:1996-07-11T04:00:00.000Z
                    string fieldValue = ( (fieldName == "OrderDate" || fieldName == "RequiredDate" || fieldName == "ShippedDate") 
                                        && field.Split(':')[1].Length != 0 && field.Split(':')[1] != "null"
                                        ? (field.Split(':')[1] + ":" + field.Split(':')[2] + ":" + field.Split(':')[3]) : field.Split(':')[1]);

                    columns.Add(fieldName, fieldValue);
                }

                Order newObj = new Order();
                newObj.OrderID = columns["OrderID"] != "null" ? Convert.ToInt16(columns["OrderID"]) : 0;
                newObj.CustomerID = columns["CustomerID"] != "null" ? columns["CustomerID"] : String.Empty;
                newObj.Freight = Convert.ToDecimal(columns["Freight"]);
                newObj.ShipCity = columns["ShipCity"];
                newObj.ShipCountry = columns["ShipCountry"];

                DateTime orderDate;
                newObj.OrderDate = DateTime.TryParseExact(columns["OrderDate"], "yyyy-MM-ddThh:mm:ss.000Z", null, System.Globalization.DateTimeStyles.None, out orderDate) ? orderDate : DateTime.MinValue;

                rVal.Add(newObj);

                columns.Clear();
            }

            return rVal;
        }

        // Read handler. Fired when when grid needs to fetch rows
        public ActionResult Read_Orders([DataSourceRequest] DataSourceRequest request, string customerId)
        {
            using (NorthwindEntities _database = new NorthwindEntities())
            {
                var orders = _database.Orders.Where(o => o.CustomerID == customerId).ToList();

                return Json(orders.ToDataSourceResult(request, e => new Order
                {
                    OrderID = e.OrderID,
                    OrderDate = e.OrderDate,
                    RequiredDate = e.RequiredDate,
                    ShippedDate = e.ShippedDate,
                    ShipVia = e.ShipVia,
                    Freight = e.Freight,
                    ShipName = e.ShipName,
                    ShipAddress = e.ShipAddress,
                    ShipCity = e.ShipCity,
                    ShipRegion = e.ShipRegion,
                    ShipPostalCode = e.ShipPostalCode,
                    ShipCountry = e.ShipCountry,

                }), JsonRequestBehavior.AllowGet);
            }
        }
    }
}
