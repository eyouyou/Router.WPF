using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF;
using Unity.UI.WPF.Handlers;
using Unity.UI.WPF.Routers;

namespace Router.WPF.Sample
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Unity.UI.WPF.Router.InitRouter("/moderation/blocked-users",
                new Route("profile", "profile", new FuncComponentHandler((param) => new TextBlock() { Text = "profile" })),
                new Route("account", "account", new FuncComponentHandler((param) => new TextBlock() { Text = "account" })),
                new Route("appearence", "appearence", new FuncComponentHandler((param) => new TextBlock() { Text = "appearence" })),
                new Route("billing and plans", "billing-and-plans", new FuncComponentHandler((param) =>
                {
                    var stackPanel = new StackPanel();
                    stackPanel.Children.Add(new TextBlock() { Text = "billing-and-plans" });
                    var btn = new Button() { Content = "navigate", Command = new RelayCommand(NavigatePlanCommand, CanExecuteButtonCommand) };
                    var btn2 = new Button() { Content = "navigate2", CommandParameter = 2222, Command = new RelayCommand(NavigatePlanCommand, CanExecuteButtonCommand) };
                    stackPanel.Children.Add(btn);
                    stackPanel.Children.Add(btn2);
                    stackPanel.Children.Add(new Outlet());
                    return stackPanel;
                }))
                {
                    Children = new List<Route> {
                        new Route("plans and usage", "plans-and-usage/{userId:int}", new WindowHandler((param) =>
                        {
                            var stack = new StackPanel();
                            stack.Children.Add(new TextBlock() { Text = $"plans and usage {param.Parameters["userId"]}" });
                            var btn = new Button { Content="navigate to blocked users",  Command = new RelayCommand(NavigateBlockUserCommand, CanExecuteButtonCommand), CommandParameter = "me" };
                            var btn2 = new Button(){ Content="navigate to plan",  Command = new RelayCommand(NavigatePlanCommand, CanExecuteButtonCommand)};

                            stack.Children.Add(btn);
                            stack.Children.Add(new Outlet());
                            stack.Children.Add(btn2);

                            return new WindowResult(param.Route.Key, stack);
                        }, multiple: true, refRoute: false))
                        {
                            Children = new List<Route> {
                                new Route("plan", "plan/{planId:int}", new FuncComponentHandler( (param)=> {
                                        var stack = new StackPanel();
                                        var btn = new Button(){ Content = "get matches" ,Command = new RelayCommand(GetMatchesCommand, CanExecuteButtonCommand)};
                                        btn.CommandParameter = btn;
                                        stack.Children.Add(new TextBlock() { Text = $"plan {param.Parameters["planId"]}" });
                                        stack.Children.Add(btn);
                                        return stack;
                                }))
                            }
                        },
                        new Route("spending limits", "spending-limits", new FuncComponentHandler( (param) => new TextBlock() { Text = "spending limits" }))
                    }
                },
                new Route("moderation", "moderation", new FuncComponentHandler((param) =>
                {
                    var stackPanel = new StackPanel();
                    stackPanel.Children.Add(new TextBlock() { Text = "moderation" });
                    stackPanel.Children.Add(new Outlet());
                    return stackPanel;
                }))
                {
                    Children = new List<Route> {
                        new Route("blocked users", "blocked-users", new FuncComponentHandler( (param) => {
                            var stackPanel = new StackPanel();
                            stackPanel.Children.Add(new TextBlock() { Text = "blocked-users" });
                            stackPanel.Children.Add(new Outlet());
                            return stackPanel;
                        }))
                        {
                            Children = new List<Route> {
                                new IndexRoute("blocked users index", new FuncComponentHandler( (param) => {
                                        var stack = new StackPanel();
                                        stack.Children.Add(new TextBlock() { Text = $"blocked user index" });
                                        stack.Children.Add(new Button(){ Content="navigate to plan xxx", CommandParameter= "111", Command = new RelayCommand(NavigatePlanCommand, CanExecuteButtonCommand)});
                                        return stack;
                                    })),
                                new Route("blocked user", "{blockedUserId:string}", new FuncComponentHandler( (param) =>
                                    {
                                        var stack = new StackPanel();
                                        var btn = new Button(){ Content = "get matches" ,Command = new RelayCommand(GetMatchesCommand, CanExecuteButtonCommand)};
                                        btn.CommandParameter = btn;
                                        stack.Children.Add(new TextBlock() { Text = $"blocked user {param.Parameters["blockedUserId"]}" });
                                        stack.Children.Add(new Button(){ Content="navigate to plan index", Command = new RelayCommand(NavigateBlockUserCommand, CanExecuteButtonCommand)});
                                        stack.Children.Add(btn);
                                        return stack;
                                    }))
                            }
                        }
                    }
                }
            );

            Content = this.CurrentRouter();
        }


        private void GetMatchesCommand(object? parameter)
        {
            // 执行命令的逻辑
            MessageBox.Show("Button clicked!");
            if (parameter is Button btn)
            {
                var matches = btn.GetRouteMatches();
                var searches = Application.Current.Router().RouteCollection.Search("moderation");
            }
        }

        private void NavigateBlockUserCommand(object? parameter)
        {
            // 执行命令的逻辑
            MessageBox.Show("Button clicked!");

            if (parameter != null)
            {
                this.CurrentRouter().Navigate($"/moderation/blocked-users/{parameter}", null);
            }
            else
            {
                this.CurrentRouter().Navigate($"/moderation/blocked-users", null);
            }
        }

        private void NavigatePlanCommand(object? parameter)
        {
            // 执行命令的逻辑
            MessageBox.Show("Button clicked!");

            if (parameter != null)
            {
                this.CurrentRouter().Navigate($"/billing-and-plans/plans-and-usage/{parameter}/plan/123", null);
            }
            else
            {
                this.CurrentRouter().Navigate("/billing-and-plans/plans-and-usage/3333/plan/123", null);
            }
        }

        private void NavigatePlanUsageCommand(object? parameter)
        {
            // 执行命令的逻辑
            MessageBox.Show("Button clicked!");

            this.CurrentRouter().Navigate("/billing-and-plans/plans-and-usage/222", null);
        }

        private bool CanExecuteButtonCommand(object? parameter)
        {
            // 决定命令是否可以执行的逻辑
            return true;
        }
    }
}
