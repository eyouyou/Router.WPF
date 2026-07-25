using System.Collections.Generic;
using System.Linq;
using Router.WPF.Sample.ViewModels;
using Router.WPF.Sample.Views;
using Router.Wpf.Abstractions;
using Router.Wpf.Handlers;

namespace Router.WPF.Sample
{
    public sealed record SidebarItem(string Glyph, string Title, string Path);
    public sealed record FeatureTab(
        string Key,
        string Title,
        IReadOnlyList<SidebarItem> Items);
    public sealed record PageItem(string Key, string Title, string Glyph);
    public sealed record LeftNavigationItem(string Key, string Title, string Glyph);
    public sealed record FeatureMatrixCell(
        string PageKey,
        string NavigationKey,
        IReadOnlyList<FeatureTab> FeatureTabs);

    public sealed record NavigationSection(
        string Key,
        string Title,
        string Glyph,
        IReadOnlyList<string> RoutePrefixes,
        IReadOnlyList<FeatureTab> FeatureTabs)
    {
        public IReadOnlyList<SidebarItem> Items =>
            FeatureTabs.SelectMany(tab => tab.Items).ToArray();
    }

    public static class AppRoutes
    {
        public const string StartupPath = "/home";

        public static readonly IReadOnlyList<SidebarItem> Sidebar = new[]
        {
            new SidebarItem("", "Home",        "/home"),
            new SidebarItem("", "Profile",     "/profile"),
            new SidebarItem("", "Account",     "/account"),
            new SidebarItem("", "Appearance",  "/appearance"),
            new SidebarItem("", "Preferences", "/preferences"),
            new SidebarItem("", "Settings",    "/settings/general"),
            new SidebarItem("", "Billing",     "/billing-and-plans/spending-limits"),
            new SidebarItem("", "Moderation",  "/moderation/blocked-users"),
        };

        public static readonly IReadOnlyList<NavigationSection> Sections = new[]
        {
            new NavigationSection(
                "overview",
                "Overview",
                "\uE80F",
                new[] { "/home" },
                new[]
                {
                    new FeatureTab(
                        "start",
                        "Start",
                        Sidebar.Where(item => item.Path == "/home").ToArray()),
                }),
            new NavigationSection(
                "account",
                "Account",
                "\uE77B",
                new[] { "/profile", "/account", "/appearance", "/preferences" },
                new[]
                {
                    new FeatureTab(
                        "identity",
                        "Identity",
                        Sidebar.Where(item =>
                            item.Path == "/profile"
                            || item.Path == "/account").ToArray()),
                    new FeatureTab(
                        "experience",
                        "Experience",
                        Sidebar.Where(item =>
                            item.Path == "/appearance"
                            || item.Path == "/preferences").ToArray()),
                }),
            new NavigationSection(
                "administration",
                "Administration",
                "\uE713",
                new[] { "/settings", "/moderation" },
                new[]
                {
                    new FeatureTab(
                        "system",
                        "System",
                        Sidebar.Where(item => item.Path.StartsWith("/settings")).ToArray()),
                    new FeatureTab(
                        "safety",
                        "Safety",
                        Sidebar.Where(item => item.Path.StartsWith("/moderation")).ToArray()),
                }),
            new NavigationSection(
                "commerce",
                "Commerce",
                "\uE8C7",
                new[] { "/billing-and-plans" },
                new[]
                {
                    new FeatureTab(
                        "billing",
                        "Billing",
                        Sidebar.Where(item => item.Path.StartsWith("/billing-and-plans")).ToArray()),
                }),
        };

        public static readonly IReadOnlyList<PageItem> MatrixPages = new[]
        {
            new PageItem("workspace", "Workspace", "\uE80F"),
            new PageItem("management", "Management", "\uE713"),
            new PageItem("commerce", "Commerce", "\uE8C7"),
        };

        public static readonly IReadOnlyList<LeftNavigationItem> MatrixNavigation = new[]
        {
            new LeftNavigationItem("personal", "Personal", "\uE77B"),
            new LeftNavigationItem("team", "Team", "\uE716"),
            new LeftNavigationItem("system", "System", "\uE770"),
        };

        public static readonly IReadOnlyList<FeatureMatrixCell> MatrixCells = new[]
        {
            Cell("workspace", "personal",
                Tab("start", "Start", "/home"),
                Tab("identity", "Identity", "/profile")),
            Cell("workspace", "team",
                Tab("collaboration", "Collaboration", "/account")),
            Cell("workspace", "system",
                Tab("experience", "Experience", "/appearance", "/preferences")),

            Cell("management", "personal",
                Tab("accounts", "Accounts", "/profile", "/account")),
            Cell("management", "team",
                Tab("safety", "Safety", "/moderation/blocked-users")),
            Cell("management", "system",
                Tab("settings", "Settings", "/settings/general")),

            Cell("commerce", "personal",
                Tab("billing", "Billing", "/billing-and-plans/spending-limits")),
            Cell("commerce", "team",
                Tab("plans", "Plans", "/billing-and-plans/spending-limits")),
            Cell("commerce", "system",
                Tab("controls", "Controls", "/billing-and-plans/spending-limits")),
        };

        private static FeatureMatrixCell Cell(
            string pageKey,
            string navigationKey,
            params FeatureTab[] tabs)
            => new(pageKey, navigationKey, tabs);

        private static FeatureTab Tab(string key, string title, params string[] paths)
            => new(
                key,
                title,
                paths.Select(path => Sidebar.First(item => item.Path == path)).ToArray());

        public static void Register()
        {
            Router.Wpf.Router.InitRouter(StartupPath,
                new Route("home", "home",
                    new FuncComponentHandler(_ => new HomeView())),

                new Route("profile", "profile",
                    new FuncComponentHandler(_ => new ProfileView())),

                new Route("account", "account",
                    new FuncComponentHandler(_ => new AccountView())),

                new Route("appearance", "appearance",
                    new FuncComponentHandler(_ => new AppearanceView())),

                // 可空参数演示：/preferences 或 /preferences/dark 都能匹
                new Route("preferences", "preferences/{theme?}",
                    new FuncComponentHandler(p => new PreferencesView
                    {
                        DataContext = new PreferencesViewModel(p.Parameters)
                    })),

                // 单窗体演示（IsMultiple=false, IsRouteRef=true）。
                // 在 /settings/general | /security | /notifications 之间切换时，
                // 同一个 Window 被复用，只有内部内容跟着换。
                // 路由离开 /settings 时窗体会被自动关闭（IsRouteRef=true）。
                new Route("settings", "settings",
                    new WindowHandler(_ => new WindowResult("Settings", new SettingsShellView()),
                        multiple: false,
                        refRoute: true))
                {
                    Children = new List<Route>
                    {
                        new Route("settings-general",       "general",
                            new FuncComponentHandler(_ => new SettingsGeneralView())),
                        new Route("settings-security",      "security",
                            new FuncComponentHandler(_ => new SettingsSecurityView())),
                        new Route("settings-notifications", "notifications",
                            new FuncComponentHandler(_ => new SettingsNotificationsView())),
                    }
                },

                // 嵌套路由 + 带 int 参数的 WindowHandler
                new Route("billing", "billing-and-plans",
                    new FuncComponentHandler(_ => new BillingView { DataContext = new BillingViewModel() }))
                {
                    Children = new List<Route>
                    {
                        new Route("plans-and-usage", "plans-and-usage/{userId:int}",
                            new WindowHandler(p => new WindowResult(
                                $"Plans · user #{p.Parameters["userId"]}",
                                new PlansAndUsageView
                                {
                                    DataContext = new PlansAndUsageViewModel(p.Parameters)
                                }),
                                multiple: true,
                                refRoute: false))
                        {
                            Children = new List<Route>
                            {
                                new Route("plan", "plan/{planId:int}",
                                    new FuncComponentHandler(p => new PlanView
                                    {
                                        DataContext = new PlanViewModel(p.Parameters)
                                    })),
                            }
                        },
                        new Route("spending-limits", "spending-limits",
                            new FuncComponentHandler(_ => new SpendingLimitsView())),
                    }
                },

                // 多层 outlet + IndexRoute + string 参数
                new Route("moderation", "moderation",
                    new FuncComponentHandler(_ => new ModerationView()))
                {
                    Children = new List<Route>
                    {
                        new Route("blocked-users", "blocked-users",
                            new FuncComponentHandler(_ => new BlockedUsersView()))
                        {
                            Children = new List<Route>
                            {
                                new IndexRoute("blocked-users-index",
                                    new FuncComponentHandler(_ => new BlockedUsersIndexView
                                    {
                                        DataContext = new BlockedUsersIndexViewModel()
                                    })),
                                new Route("blocked-user", "{blockedUserId:string}",
                                    new FuncComponentHandler(p => new BlockedUserView
                                    {
                                        DataContext = new BlockedUserViewModel(p.Parameters)
                                    })),
                            }
                        }
                    }
                }
            );
        }
    }
}
