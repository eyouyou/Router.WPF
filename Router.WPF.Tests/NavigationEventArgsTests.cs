using System.Collections.Generic;
using System.Windows.Controls;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Handlers;
using Xunit;

namespace Router.Wpf.Tests
{
    public class NavigationEventArgsTests
    {
        [WpfFact]
        public void Exposes_leaf_route_parameters_history_and_instance_metadata()
        {
            var route = new Route(
                "user-details",
                "users/{userId}",
                new FuncComponentHandler(_ => new UserControl()));
            var match = new RouteMatch(
                route,
                new PathMatch(
                    "/users/1001",
                    new Dictionary<string, object> { ["userId"] = 1001 }));
            var instance = new RouteInstanceMetadata(
                "user-editor",
                "Alice",
                new { Mode = "edit" });

            var args = new NavigationEventArgs(
                "/users/1001",
                instance,
                new[] { match },
                addedToHistory: false);

            Assert.Same(route, args.Route);
            Assert.Equal("user-details", args.RouteKey);
            Assert.Equal(1001, args.Parameters["userId"]);
            Assert.False(args.AddedToHistory);
            Assert.Same(instance, args.RouteInstance);
        }

        [Fact]
        public void Legacy_constructor_shape_remains_supported()
        {
            var payload = new object();
            var args = new NavigationEventArgs("/home", payload);

            Assert.Equal("/home", args.Target);
            Assert.Same(payload, args.ExtraData);
            Assert.Empty(args.Matches);
            Assert.Null(args.Route);
            Assert.Null(args.RouteKey);
            Assert.Empty(args.Parameters);
            Assert.True(args.AddedToHistory);
        }
    }
}
