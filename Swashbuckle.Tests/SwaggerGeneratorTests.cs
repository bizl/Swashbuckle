﻿using System;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Description;
using NUnit.Framework;
using Swashbuckle.Models;
using Swashbuckle.TestApp.App_Start;
using Swashbuckle.TestApp.SwaggerExtensions;

namespace Swashbuckle.Tests
{
    public class SwaggerGeneratorTests
    {
        private SwaggerSpec _swaggerSpec;

        [SetUp]
        public void Setup()
        {
            SwaggerSpecConfig.Customize(c =>
                {
                    c.ResolveBasePath(() => "http://tempuri.org");
                    c.PostFilter(new AddErrorCodeFilter(200, "It's all good!"));
                    c.PostFilter(new AddErrorCodeFilter(400, "Something's up!"));
                });

            // Get ApiExplorer for TestApp
            var httpConfiguration = new HttpConfiguration();
            WebApiConfig.Register(httpConfiguration);
            var apiExplorer = new ApiExplorer(httpConfiguration);

            _swaggerSpec = SwaggerGenerator.Instance.Generate(apiExplorer);
        }

        [Test]
        public void It_should_generate_a_listing_according_to_provided_strategy()
        {
            // e.g. Uses ControllerName by default
            var resourceListing = _swaggerSpec.Listing;
            Assert.AreEqual("1.0", resourceListing.ApiVersion);
            Assert.AreEqual("1.2", resourceListing.SwaggerVersion);
            Assert.AreEqual(3, resourceListing.Apis.Count());

            Assert.IsTrue(resourceListing.Apis.Any(a => a.Path == "/Orders"),
                "Orders declaration not listed");
            Assert.IsTrue(resourceListing.Apis.Any(a => a.Path == "/OrderItems"),
                "OrderItems declaration not listed");
            Assert.IsTrue(resourceListing.Apis.Any(a => a.Path == "/Customers"),
                "Customers declaration not listed");
        }

        [Test]
        public void It_should_generate_declarations_according_to_provided_strategy()
        {
            ApiDeclaration("/Orders", dec =>
                {
                    Assert.AreEqual("1.2", dec.SwaggerVersion);
                    Assert.AreEqual("http://tempuri.org", dec.BasePath);
                    Assert.AreEqual("/Orders", dec.ResourcePath);
                });

            ApiDeclaration("/OrderItems", dec =>
                {
                    Assert.AreEqual("1.2", dec.SwaggerVersion);
                    Assert.AreEqual("http://tempuri.org", dec.BasePath);
                    Assert.AreEqual("/OrderItems", dec.ResourcePath);
                });

            ApiDeclaration("/Customers", dec =>
                {
                    Assert.AreEqual("1.2", dec.SwaggerVersion);
                    Assert.AreEqual("http://tempuri.org", dec.BasePath);
                    Assert.AreEqual("/Customers", dec.ResourcePath);
                });
        }

        [Test]
        public void It_should_generate_an_api_spec_for_each_url_in_a_declaration()
        {
            ApiDeclaration("/Orders", dec =>
                {
                    // 3: /api/orders, /api/orders?foo={foo}&bar={bar}, /api/orders/{id}
                    Assert.AreEqual(3, dec.Apis.Count);

                    ApiSpec(dec, "/api/orders", 0, api => Assert.IsNull(api.Description));
                    ApiSpec(dec, "/api/orders", 1, api => Assert.IsNull(api.Description));
                    ApiSpec(dec, "/api/orders/{id}", 0, api => Assert.IsNull(api.Description));
                });

            ApiDeclaration("/OrderItems", dec =>
                {
                    // 2: /api/orders/{orderId}/items/{id}, /api/orders/{orderId}/items?category={category}
                    Assert.AreEqual(2, dec.Apis.Count);

                    ApiSpec(dec, "/api/orders/{orderId}/items/{id}", 0, api => Assert.IsNull(api.Description));
                    ApiSpec(dec, "/api/orders/{orderId}/items", 0, api => Assert.IsNull(api.Description));
                });

            ApiDeclaration("/Customers", dec =>
                {
                    // 2: /api/customers
                    Assert.AreEqual(1, dec.Apis.Count);

                    ApiSpec(dec, "/api/customers", 0, api => Assert.IsNull(api.Description));
                });
        }

        [Test]
        public void It_should_generate_an_operation_spec_for_each_supported_method_on_a_url()
        {
            ApiSpec("/Orders", "/api/orders", 0, api =>
                {
                    // 2: POST /api/orders, GET /api/orders
                    Assert.AreEqual(2, api.Operations.Count);

                    OperationSpec(api, "POST", operation =>
                        {
                            Assert.AreEqual("Orders_Post", operation.Nickname);
                            Assert.AreEqual("Documentation for 'Post'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("Order", operation.Type);
                        });

                    OperationSpec(api, "GET", operation =>
                        {
                            Assert.AreEqual("Orders_GetAll", operation.Nickname);
                            Assert.AreEqual("Documentation for 'GetAll'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("List[Order]", operation.Type);
                        });
                });

            ApiSpec("/Orders", "/api/orders", 1, api =>
                {
                    // 1: GET /api/orders?foo={foo}&bar={bar}
                    Assert.AreEqual(1, api.Operations.Count);

                    OperationSpec(api, "GET", operation =>
                        {
                            Assert.AreEqual("Orders_GetByParams", operation.Nickname);
                            Assert.AreEqual("Documentation for 'GetByParams'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("List[Order]", operation.Type);
                        });
                });

            ApiSpec("/Orders", "/api/orders/{id}", 0, api =>
                {
                    // 1: DELETE /api/orders/{id}
                    Assert.AreEqual(1, api.Operations.Count);

                    OperationSpec(api, "DELETE", operation =>
                        {
                            Assert.AreEqual("Orders_Delete", operation.Nickname);
                            Assert.AreEqual("Documentation for 'Delete'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("void", operation.Type);
                        });
                });

            ApiSpec("/OrderItems", "/api/orders/{orderId}/items/{id}", 0, api =>
                {
                    // 1: GET /api/orders/{orderId}/items/{id}
                    Assert.AreEqual(1, api.Operations.Count);

                    OperationSpec(api, "GET", operation =>
                        {
                            Assert.AreEqual("OrderItems_GetById", operation.Nickname);
                            Assert.AreEqual("Documentation for 'GetById'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("OrderItem", operation.Type);
                        });
                });

            ApiSpec("/OrderItems", "/api/orders/{orderId}/items", 0, api =>
                {
                    // 1: GET /api/orders/{orderId}/items?category={category}
                    Assert.AreEqual(1, api.Operations.Count);

                    OperationSpec(api, "GET", operation =>
                        {
                            Assert.AreEqual("OrderItems_GetAll", operation.Nickname);
                            Assert.AreEqual("Documentation for 'GetAll'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.AreEqual("List[OrderItem]", operation.Type);
                        });
                });

            ApiSpec("/Customers", "/api/customers", 0, api =>
                {
                    // 1: GET /api/customers
                    Assert.AreEqual(1, api.Operations.Count);

                    OperationSpec(api, "GET", operation =>
                        {
                            Assert.AreEqual("Customers_GetAll", operation.Nickname);
                            Assert.AreEqual("Documentation for 'GetAll'.", operation.Summary);
                            Assert.IsNull(operation.Notes);
                            Assert.IsNull(operation.Type);
                        });
                });
        }

        [Test]
        public void It_should_generate_a_parameter_spec_for_each_parameter_in_a_given_operation()
        {
            OperationSpec("/Orders", "/api/orders", 0, "POST", operation =>
                {
                    Assert.AreEqual(1, operation.Parameters.Count);

                    ParameterSpec(operation, "order", parameter =>
                        {
                            Assert.AreEqual("body", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'order'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("Order", parameter.Type);
                        });
                });

            OperationSpec("/Orders", "/api/orders", 0, "GET", operation =>
                Assert.AreEqual(0, operation.Parameters.Count));

            OperationSpec("/Orders", "/api/orders", 1, "GET", operation =>
                {
                    Assert.AreEqual(2, operation.Parameters.Count);

                    ParameterSpec(operation, "foo", parameter =>
                        {
                            Assert.AreEqual("query", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'foo'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("string", parameter.Type);
                        });

                    ParameterSpec(operation, "bar", parameter =>
                        {
                            Assert.AreEqual("query", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'bar'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("string", parameter.Type);
                        });
                });

            OperationSpec("/Orders", "/api/orders/{id}", 0, "DELETE", operation =>
                {
                    Assert.AreEqual(1, operation.Parameters.Count);

                    ParameterSpec(operation, "id", parameter =>
                        {
                            Assert.AreEqual("path", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'id'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("int", parameter.Type);
                        });
                });

            OperationSpec("/OrderItems", "/api/orders/{orderId}/items/{id}", 0, "GET", operation =>
                {
                    Assert.AreEqual(2, operation.Parameters.Count);

                    ParameterSpec(operation, "orderId", parameter =>
                        {
                            Assert.AreEqual("path", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'orderId'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("int", parameter.Type);
                        });

                    ParameterSpec(operation, "id", parameter =>
                        {
                            Assert.AreEqual("path", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'id'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("int", parameter.Type);
                        });
                });

            OperationSpec("/OrderItems", "/api/orders/{orderId}/items", 0, "GET", operation =>
                {
                    Assert.AreEqual(2, operation.Parameters.Count);

                    ParameterSpec(operation, "orderId", parameter =>
                        {
                            Assert.AreEqual("path", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'orderId'.", parameter.Description);
                            Assert.AreEqual(true, parameter.Required);
                            Assert.AreEqual("int", parameter.Type);
                        });

                    ParameterSpec(operation, "category", parameter =>
                        {
                            Assert.AreEqual("query", parameter.ParamType);
                            Assert.AreEqual("Documentation for 'category'.", parameter.Description);
                            Assert.AreEqual(false, parameter.Required);
                            Assert.AreEqual("string", parameter.Type);
                        });
                });

            OperationSpec("/Customers", "/api/customers", 0, "GET", operation =>
                Assert.AreEqual(0, operation.Parameters.Count));
        }

//        [Test]
//        public void It_should_generate_a_model_spec_for_all_complex_types_in_a_declaration()
//        {
//            ApiDeclaration("/Orders", dec =>
//            {
//                // 1: Order
//                Assert.AreEqual(1, dec.Models.Count);
//
//                Model(dec, "Order", model =>
//                    {
//                        ModelProperty(model, "Id", property =>
//                            {
//                                Assert.AreEqual("int", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                        ModelProperty(model, "Description", property =>
//                            {
//                                Assert.AreEqual("string", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                        ModelProperty(model, "Total", property =>
//                            {
//                                Assert.AreEqual("double", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                    });
//            });
//
//            ApiDeclaration("/OrderItems", dec =>
//                {
//                    // 1: OrderItem
//                    Assert.AreEqual(1, dec.Models.Count);
//
//                    Model(dec, "OrderItem", model =>
//                        {
//                            ModelProperty(model, "LineNo", property =>
//                            {
//                                Assert.AreEqual("int", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                            
//                            ModelProperty(model, "Product", property =>
//                            {
//                                Assert.AreEqual("string", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                            
//                            ModelProperty(model, "Category", property =>
//                            {
//                                Assert.AreEqual("string", property.type);
//                                Assert.AreEqual(true, property.required);
//                                Assert.AreEqual("LIST", property.allowableValues.valueType);
//                            
//                                Assert.IsInstanceOf<EnumeratedValuesSpec>(property.allowableValues);
//                                var values = (property.allowableValues as EnumeratedValuesSpec).values.ToArray();
//                                Assert.AreEqual("Category1", values[0]);
//                                Assert.AreEqual("Category2", values[1]);
//                                Assert.AreEqual("Category3", values[2]);
//                            });
//                            
//                            ModelProperty(model, "Quantity", property =>
//                            {
//                                Assert.AreEqual("int", property.type);
//                                Assert.AreEqual(true, property.required);
//                            });
//                        });
//                });
//
//            ApiDeclaration("/Customers", dec => Assert.AreEqual(0, dec.Models.Count));
//        }

        [Test]
        public void It_should_apply_any_provided_operation_spec_filters()
        {
            // e.g. error code filters (see Setup)
            var resourceListing = _swaggerSpec.Listing;
            foreach (var path in resourceListing.Apis.Select(a => a.Path))
            {
                foreach (var api in _swaggerSpec.Declarations[path].Apis)
                {
                    foreach (var operation in api.Operations)
                    {
                        Assert.AreEqual(2, operation.ResponseMessages.Count);
                    }
                }
            }
        }

        private void ApiDeclaration(string resourcePath, Action<ApiDeclaration> applyAssertions)
        {
            var declaration = _swaggerSpec.Declarations[resourcePath];
            applyAssertions(declaration);
        }

        private void ApiSpec(ApiDeclaration declaration, string apiPath, int index, Action<ApiSpec> applyAssertions)
        {
            var apiSpec = declaration.Apis
                .Where(api => api.Path == apiPath)
                .ElementAt(index);

            applyAssertions(apiSpec);
        }

        private void ApiSpec(string resourcePath, string apiPath, int index, Action<ApiSpec> applyAssertions)
        {
            var apiSpec = _swaggerSpec.Declarations[resourcePath].Apis
                .Where(api => api.Path == apiPath)
                .ElementAt(index);

            applyAssertions(apiSpec);
        }

        private void OperationSpec(ApiSpec api, string httpMethod, Action<OperationSpec> applyAssertions)
        {
            var operationSpec = api.Operations.Single(op => op.Method == httpMethod);
            applyAssertions(operationSpec);
        }

        private void OperationSpec(string resourcePath, string apiPath, int index, string httpMethod,
            Action<OperationSpec> applyAssertions)
        {
            var apiSpec = _swaggerSpec.Declarations[resourcePath].Apis
                .Where(api => api.Path == apiPath)
                .ElementAt(index);

            var operationSpec = apiSpec.Operations.Single(op => op.Method == httpMethod);
            applyAssertions(operationSpec);
        }

        private void ParameterSpec(OperationSpec operation, string name, Action<ParameterSpec> applyAssertions)
        {
            var parameterSpec = operation.Parameters.Single(param => param.Name == name);
            applyAssertions(parameterSpec);
        }
    }
}