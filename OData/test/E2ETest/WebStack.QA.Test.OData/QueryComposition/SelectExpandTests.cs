﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Dispatcher;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.OData;
using System.Web.OData.Builder;
using System.Web.OData.Extensions;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using Nuwa;
using WebStack.QA.Common.XUnit;
using WebStack.QA.Test.OData.Common;
using Xunit;
using Xunit.Extensions;

namespace WebStack.QA.Test.OData.QueryComposition
{
    public class SelectExpandTests : ODataTestBase
    {
        private const string AutoExpandTestUrl = "{0}/selectexpand/AutoExpandCustomer(1)";
        
        public static TheoryDataSet<string, int> AutoExpandTestData
        {
            get
            {
                return new TheoryDataSet<string, int>
                {
                    {AutoExpandTestUrl + "?$select=Stuff", 1},
                    {AutoExpandTestUrl + "?$select=Id", 2},
                    {AutoExpandTestUrl + "?$expand=Stuff&$select=Id", 2},
                    {AutoExpandTestUrl + "?$expand=NormalStuff", 3},
                    {AutoExpandTestUrl, 2},
                };
            }
        }

        [NuwaConfiguration]
        public static void UpdateConfiguration(HttpConfiguration configuration)
        {
            configuration.Services.Replace(
                  typeof(IAssembliesResolver),
                  new TestAssemblyResolver(
                      typeof(SelectCustomerController),
                      typeof(AutoExpandCustomerController)));
            configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
            configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            configuration.MapODataServiceRoute("selectexpand", "selectexpand", GetEdmModel(configuration));
        }

        private static IEdmModel GetEdmModel(HttpConfiguration configuration)
        {
            var builder = new ODataConventionModelBuilder(configuration);

            EntitySetConfiguration<SelectCustomer> customers = builder.EntitySet<SelectCustomer>("SelectCustomer");
            customers.EntityType.Action("CreditRating").Returns<double>();
            customers.EntityType.Collection.Action("PremiumCustomers").ReturnsCollectionFromEntitySet<SelectCustomer>("SelectCustomer");

            builder.EntitySet<AutoExpandCustomer>("AutoExpandCustomer");
            builder.EntitySet<SelectOrderDetail>("SelectOrderDetail");
            builder.EntityType<SelectPremiumCustomer>();
            builder.EntitySet<SelectOrder>("SelectOrder");
            builder.EntitySet<SelectBonus>("SelectBonus");
            builder.EntitySet<AutoExpandStuff>("Stuff");

            IEdmModel model = builder.GetEdmModel();
            return model;
        }

        [Fact]
        public void QueryJustThePropertiesOfTheEntriesOnAFeed()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/?$select=*", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=full"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().All(p => p.Name != "#Container.CreditRating")));
        }

        [Fact]
        public void QueryJustTheActionsOfTheEntriesOnAFeed()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/?$select=Default.*", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=full"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Any(p => p.Name == "#Default.CreditRating")));
        }

        [Fact]
        public void QueryASubsetOfThePropertiesOfAnEntry()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/?$select=Name", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata=minimalmetadata"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 1 && x.Properties().All(p => p.Name == "Name")));
        }

        [Fact]
        public void QueryASubsetOfThePropertiesOfAnEntryAndASubsetOfThePropertiesOfARelatedEntry()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/?$select=Id,Name&$expand=SelectOrders($select=Id)", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=minimal"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 3));
            Assert.True(customers.OfType<JObject>().All(x => (int)x["Id"] == ((JArray)x["SelectOrders"]).Count));
        }

        [Fact]
        public void QueryASubSetOfThePropertiesPresentOnlyInADerivedEntry()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/Default.PremiumCustomers?" +
                "$select=Id,WebStack.QA.Test.OData.QueryComposition.SelectPremiumCustomer/Category", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            var content = response.Content.ReadAsAsync<JObject>().Result;

            JArray customers = content["value"] as JArray;
            Assert.Equal(10, customers.Count);
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 2));
        }

        [Fact]
        public void QueryAnEntryAndIncludeTheRelatedEntriesForAGivenNavigationPropertyInline()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer?$select=Id&$expand=SelectOrders", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 2));
            foreach (JObject customer in (IEnumerable<JToken>)customers)
            {
                JArray orders = customer["SelectOrders"] as JArray;
                Assert.Equal((int)customer["Id"], orders.Count);
                foreach (JObject order in (IEnumerable<JToken>)orders)
                {
                    Assert.Equal(3, order.Properties().Count());
                }
            }
        }

        [Fact]
        public void QueryForAnEntryAnIncludeTheRelatedEntriesForASetOfNavigationProperties()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/Default.PremiumCustomers?" +
                "$select=Id&" +
                "$expand=SelectOrders,WebStack.QA.Test.OData.QueryComposition.SelectPremiumCustomer/Bonuses", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            var content = response.Content.ReadAsAsync<JObject>().Result;

            JArray customers = content["value"] as JArray;
            Assert.Equal(10, customers.Count);
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 3));
            foreach (JObject customer in (IEnumerable<JToken>)customers)
            {
                JArray orders = customer["SelectOrders"] as JArray;
                Assert.Equal((int)customer["Id"], orders.Count);
                foreach (JObject order in (IEnumerable<JToken>)orders)
                {
                    Assert.Equal(3, order.Properties().Count());
                }

                JArray bonuses = customer["Bonuses"] as JArray;
                Assert.Equal((int)customer["Id"], bonuses.Count);
                foreach (JObject bonus in (IEnumerable<JToken>)bonuses)
                {
                    Assert.Equal(2, bonus.Properties().Count());
                }
            }
        }

        [Fact]
        public void QueryForAnEntryAndIncludeTheRelatedEntriesForAGivenNavigationPropertyPath()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer?$select=Id,SelectOrders&$expand=SelectOrders($expand=OrderDetails)", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
            JObject result;
            string content;

            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            content = response.Content.ReadAsStringAsync().Result;

            result = JObject.Parse(content);
            Assert.NotNull(result);
            JsonAssert.ArrayLength(10, "value", result);
            JArray customers = (JArray)result["value"];
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 2));
            foreach (JObject customer in (IEnumerable<JToken>)customers)
            {
                JArray orders = customer["SelectOrders"] as JArray;
                Assert.Equal((int)customer["Id"], orders.Count);
                foreach (JObject order in (IEnumerable<JToken>)orders)
                {
                    Assert.Equal(4, order.Properties().Count());
                    JArray orderDetails = order["OrderDetails"] as JArray;
                    Assert.Equal((int)order["Id"], orderDetails.Count);
                    foreach (JObject orderDetail in (IEnumerable<JToken>)orderDetails)
                    {
                        Assert.Equal(4, orderDetail.Properties().Count());
                    }
                }
            }
        }

        [Fact]
        public void QueryForAnEntryAnIncludeTheRelatedEntriesForANavigationPropertyPresentOnlyInDerivedEntries()
        {
            string queryUrl = string.Format("{0}/selectexpand/SelectCustomer/Default.PremiumCustomers?" +
                "$select=Id&" +
                "$expand=WebStack.QA.Test.OData.QueryComposition.SelectPremiumCustomer/Bonuses", BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;
           
            response = client.SendAsync(request).Result;

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);
            var content = response.Content.ReadAsAsync<JObject>().Result;

            JArray customers = content["value"] as JArray;
            Assert.Equal(10, customers.Count);
            Assert.True(customers.OfType<JObject>().All(x => x.Properties().Count() == 2));
            foreach (JObject customer in (IEnumerable<JToken>)customers)
            {
                JArray bonuses = customer["Bonuses"] as JArray;
                Assert.Equal((int)customer["Id"], bonuses.Count);
                foreach (JObject bonus in (IEnumerable<JToken>)bonuses)
                {
                    Assert.Equal(2, bonus.Properties().Count());
                }
            }
        }

        [PropertyData("AutoExpandTestData")]
        public void QueryForAnEntryIncludeTheAutoExpandNavigationProperty(string url, int propCount)
        {
            // Arrange
            string queryUrl = string.Format(url, BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=none"));
            HttpClient client = new HttpClient();
            HttpResponseMessage response;

            // Act
            response = client.SendAsync(request).Result;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Content);

            var customer = response.Content.ReadAsAsync<JObject>().Result;
            Assert.Equal(customer.Properties().Count(), propCount);

            JObject stuff = customer["Stuff"] as JObject;
            Assert.NotNull(stuff);

            JObject bonus = stuff["Bonus"] as JObject;
            Assert.Equal((int)stuff["Id"], bonus["Id"]);
            Assert.Equal((int)stuff["Id"] * 1000, bonus["Ammount"]);
        }
    }

    public class SelectCustomerController : ODataController
    {
        public IList<SelectCustomer> Customers { get; set; }

        public SelectCustomerController()
        {
            Customers = Enumerable.Range(0, 10).Select(i => new SelectCustomer
            {
                Id = i,
                Name = string.Format("Customer{0}", i),
                SelectOrders = Enumerable.Range(0, i).Select(j => new SelectOrder
                {
                    Id = j,
                    Date = DateTime.Now.Subtract(TimeSpan.FromDays(j)),
                    BillingAddress = new SelectAddress
                    {
                        FirstLine = string.Format("First line {0}", j),
                        SecondLine = string.Format("Second line {0}", j),
                        ZipCode = j * 100,
                        City = string.Format("City {0}", j),
                        State = string.Format("State {0}", j),
                        Country = string.Format("Country {0}", j),
                    },
                    OrderDetails = Enumerable.Range(0, j).Select(k => new SelectOrderDetail
                    {
                        Id = k,
                        Ammount = k,
                        Name = string.Format("Name {0}", k),
                        Price = k * 1000
                    }).ToList()
                }).ToList()
            }).ToList();
        }

        [EnableQuery]
        public IQueryable<SelectCustomer> Get()
        {
            return Customers.AsQueryable();
        }

        [EnableQuery]
        public IQueryable<SelectCustomer> PremiumCustomers()
        {
            return Enumerable.Range(0, 10).Select(i => new SelectPremiumCustomer
            {
                Id = i,
                Name = string.Format("Customer{0}", i),
                Category = string.Format("Category{0}", i),
                Bonuses = Enumerable.Range(0, i).Select(j => new SelectBonus
                {
                    Id = j,
                    Ammount = j * 1000
                }).ToList(),
                SelectOrders = Enumerable.Range(0, i).Select(j => new SelectOrder
                {
                    Id = j,
                    Date = DateTime.Now.Subtract(TimeSpan.FromDays(j)),
                    BillingAddress = new SelectAddress
                    {
                        FirstLine = string.Format("First line {0}", j),
                        SecondLine = string.Format("Second line {0}", j),
                        ZipCode = j * 100,
                        City = string.Format("City {0}", j),
                        State = string.Format("State {0}", j),
                        Country = string.Format("Country {0}", j),
                    },
                    OrderDetails = new List<SelectOrderDetail>()
                }).ToList(),

            }).AsQueryable();
        }

        public double CreditRating(int key)
        {
            return 0;
        }
    }

    public class AutoExpandCustomerController : ODataController
    {
        public IList<AutoExpandCustomer> Customers { get; set; }

        public AutoExpandCustomerController()
        {
            Customers = Enumerable.Range(0, 10).Select(i => new AutoExpandCustomer
            {
                Id = i,
                Stuff = new AutoExpandStuff
                {
                    Id = i,
                    Bonus = new SelectBonus
                    {
                        Id = i,
                        Ammount = i * 1000
                    }
                }
            }).ToList();
        }

        [EnableQuery]
        public IQueryable<AutoExpandCustomer> Get()
        {
            return Customers.AsQueryable();
        }

        [EnableQuery]
        public AutoExpandCustomer Get(int key)
        {
            return Customers.ElementAt(key);
        }
    }

    public class AutoExpandCustomer
    {
        public int Id { get; set; }

        [AutoExpand]
        public AutoExpandStuff Stuff { get; set; }

        public AutoExpandStuff NormalStuff { get; set; }
    }

    public class AutoExpandStuff
    {
        public int Id { get; set; }

        [AutoExpand]
        public SelectBonus Bonus { get; set; }
    }

    public class SelectCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual IList<SelectOrder> SelectOrders { get; set; }
    }

    public class SelectPremiumCustomer : SelectCustomer
    {
        public string Category { get; set; }
        public IList<SelectBonus> Bonuses { get; set; }
    }

    public class SelectBonus
    {
        public int Id { get; set; }
        public double Ammount { get; set; }
    }

    public class SelectOrder
    {
        public int Id { get; set; }
        public DateTimeOffset Date { get; set; }
        public SelectAddress BillingAddress { get; set; }
        public virtual IList<SelectOrderDetail> OrderDetails { get; set; }
    }

    public class SelectAddress
    {
        public string FirstLine { get; set; }
        public string SecondLine { get; set; }
        public int ZipCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
    }

    public class SelectOrderDetail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Ammount { get; set; }
        public double Price { get; set; }
    }
}
