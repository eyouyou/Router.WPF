using System.Collections.Generic;
using Router.WPF.Sample.ViewModels;
using Router.WPF.Sample.Views;
using Router.Wpf.Abstractions;
using Router.Wpf.Handlers;

namespace Router.WPF.Sample
{
    public sealed record SidebarItem(string Glyph, string Title, string Path);

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
