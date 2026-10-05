using System.Windows;
using CommunityToolkit.WinUI.Notifications;
namespace ZmanLenagen.App;
public partial class App : Application {
 private Mutex? mutex;
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  if(e.Args.Contains("--unregister")) {ToastNotificationManagerCompat.Uninstall(); Shutdown();return;}
  mutex=new Mutex(true,@"Local\ZmanLenagen-"+System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value,out var first);
  if(!first) { MessageBox.Show("זמן לנגן כבר פתוחה. אפשר למצוא אותה בשורת המשימות."); Shutdown();return; }
  DispatcherUnhandledException+=(s,a)=>{MessageBox.Show("לא הצלחנו להשלים את הפעולה. הנתונים שנשמרו נשארו בטוחים.\n"+a.Exception.Message,"זמן לנגן");a.Handled=true;};
  var window=new MainWindow();MainWindow=window;
  ToastNotificationManagerCompat.OnActivated+=a=>Dispatcher.Invoke(()=>{window.Show();window.WindowState=WindowState.Normal;window.Activate();});
  window.Show();
 }
 protected override void OnExit(ExitEventArgs e) {mutex?.Dispose();base.OnExit(e);}
}
