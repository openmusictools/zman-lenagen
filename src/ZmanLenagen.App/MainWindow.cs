using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using CommunityToolkit.WinUI.Notifications;
using ZmanLenagen.Core;
namespace ZmanLenagen.App;
public sealed class MainWindow : Window {
 readonly Store store=new();
 readonly StackPanel page=new(){Margin=new Thickness(28)};
 readonly DispatcherTimer heartbeat=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly Stopwatch clock=new();
 RunState? run;
 DateTimeOffset sliceStart;
 TextBlock? timerText, statusText;
 Button? pauseButton;
 readonly List<(TextBox tag,TextBox minutes)> rows=[];
 List<List<Exercise>> circuits;
 List<string> tags;
 List<int> minutes;
 bool closing;
 readonly PracticeTools tools;
 public MainWindow() {
  Title="זמן לנגן";Width=1040;Height=920;MinWidth=980;MinHeight=760;
  FlowDirection=FlowDirection.RightToLeft;FontFamily=new FontFamily("Segoe UI");FontSize=16;
  Background=Brush("#F7F6FF");Foreground=Brush("#292742");
  tools=new PracticeTools(store,()=>run?.Step,Recordings);
  var layout=new DockPanel();DockPanel.SetDock(tools,Dock.Bottom);layout.Children.Add(tools);layout.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=layout;
  circuits=store.Get<List<List<Exercise>>>("circuits")??[];tags=store.Get<List<string>>("tags")??[];minutes=store.Get<List<int>>("minutes")??[];
  run=store.Get<RunState>("run");
  if(run!=null) {
   if(run.AwaitingFeedback && run.Step?.Feedback!=null) {run.Index++;run.Step=null;run.AwaitingFeedback=false;}
   run.Paused=true;store.Save(run,run.Step);
  }
  heartbeat.Tick+=(s,e)=>Tick();heartbeat.Start();
  SystemEvents.SessionSwitch+=SessionSwitch;SystemEvents.PowerModeChanged+=PowerChanged;
  Closing+=(s,e)=>{
   if(closing)return;
   if(run?.Step!=null&&!run.AwaitingFeedback&&!run.Paused) Pause();
   if(run!=null && MessageBox.Show("לסגור עכשיו? המעגל יישמר ותוכל להמשיך ממנו בפעם הבאה.","נתראה בקרוב",MessageBoxButton.YesNo)!=MessageBoxResult.Yes){e.Cancel=true;return;}
   closing=true;tools.Dispose();heartbeat.Stop();SystemEvents.SessionSwitch-=SessionSwitch;SystemEvents.PowerModeChanged-=PowerChanged;store.Dispose();
  };
  Home();
 }
 static SolidColorBrush Brush(string color)=>(SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
 TextBlock Text(string value,int size=16)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5,8,5,8)};
 Button Button(string caption,Action click,string? color=null) {var b=new Button{Content=caption};if(color!=null)b.Background=Brush(color);b.Click+=(s,e)=>Guard(click);return b;}
 void Guard(Action action) {try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"לא הצלחנו להשלים את הפעולה");}}
 void Clear(string title,string subtitle) {
  timerText=null;statusText=null;pauseButton=null;page.Children.Clear();
  page.Children.Add(Text(title,32));page.Children.Add(Text(subtitle));
  page.Opacity=0;page.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
 }
 void Home() {
  Clear("זמן לנגן 🎵","כל אימון הוא עוד צעד. בוא ניתן מקום לנגינה שלך.");
  var actions=new WrapPanel();page.Children.Add(actions);
  if(run!=null) actions.Children.Add(Button("המשך המעגל השמור",ShowRun,"#BCEEDB"));
  else actions.Children.Add(Button("בוא נתחיל את האימון",()=>Editor(),"#BCEEDB"));
  actions.Children.Add(Button("הורד את כל הנתונים",Export));actions.Children.Add(Button("ייבוא גיבוי",Import));
  actions.Children.Add(Button("כל ההקלטות",Recordings));actions.Children.Add(Button("ניהול נתונים",Manage));
  var steps=store.Steps();var daily=Metrics.Daily(steps,TimeZoneInfo.Local);var today=DateOnly.FromDateTime(DateTime.Now);
  var last=steps.FirstOrDefault(x=>x.Feedback!=null);
  var lastSession=last==null?new List<PracticeStep>():steps.Where(x=>x.SessionId==last.SessionId).ToList();
  page.Children.Add(Text(last==null?"האימון הראשון שלך מחכה לך":$"הסבב האחרון: {lastSession.Min(x=>x.Started).ToLocalTime():dd/MM/yyyy HH:mm} · {Duration(lastSession.Sum(x=>x.CreditedSeconds))} תרגול · {lastSession.Count} שלבים",19));
  var cards=new WrapPanel();page.Children.Add(cards);
  foreach(var days in new[]{7,30}) {
   var start=today.AddDays(1-days);var previous=start.AddDays(-days);
   var total=daily.Where(x=>x.Key>=start&&x.Key<=today).Sum(x=>x.Value);
   var before=daily.Where(x=>x.Key>=previous&&x.Key<start).Sum(x=>x.Value);
   var active=daily.Count(x=>x.Key>=start&&x.Key<=today&&x.Value>0);
   var sessions=steps.Where(x=>Metrics.Daily([x],TimeZoneInfo.Local).Any(d=>d.Key>=start&&d.Key<=today&&d.Value>0)).Select(x=>x.SessionId).Distinct().Count();
   cards.Children.Add(new Border{Background=Brush("#FFFFFF"),CornerRadius=new CornerRadius(18),Padding=new Thickness(16),Margin=new Thickness(5),Width=290,Child=Text($"{days} ימים אחרונים\n{Duration(total)} תרגול\n{active} ימי תרגול · {sessions} סבבים\nבתקופה הקודמת: {Duration(before)}",18)});
  }
  if(steps.Any(s=>s.Feedback==null)) page.Children.Add(Text("יש שלבים ללא משוב. הם שמורים בהיסטוריה ואינם נספרים כתרגול."));
  page.Children.Add(Text("30 הימים האחרונים — כולל ימים ללא תרגול",21));
  Chart(Enumerable.Range(0,30).Select(i=>today.AddDays(i-29)).Select(d=>(d.ToString("dd/MM"),daily.GetValueOrDefault(d))).ToList());
  page.Children.Add(Text("המבט החודשי — 12 החודשים האחרונים",21));
  var month=new DateOnly(today.Year,today.Month,1);
  Chart(Enumerable.Range(0,12).Select(i=>month.AddMonths(i-11)).Select(m=>(m.ToString("MM/yy"),daily.Where(d=>d.Key.Year==m.Year&&d.Key.Month==m.Month).Sum(d=>d.Value))).ToList());
  page.Children.Add(Text("לפי תגיות — 30 ימים אחרונים",21));
  foreach(var group in steps.GroupBy(s=>s.Tag).OrderByDescending(g=>g.Sum(s=>s.CreditedSeconds))) {
   var seconds=Metrics.Daily(group,TimeZoneInfo.Local).Where(x=>x.Key>=today.AddDays(-29)&&x.Key<=today).Sum(x=>x.Value);
   if(seconds>0) page.Children.Add(Text($"{group.Key}: {Duration(seconds)}"));
  }
  page.Children.Add(Text("היסטוריית האימונים",21));
  foreach(var s in steps.Take(60)) {
   var box=new StackPanel();box.Children.Add(Text($"התחלה: {s.Started.ToLocalTime():dd/MM/yyyy HH:mm}\nתכנון: {s.PlannedMinutes} דקות · נמדד: {Duration(s.MeasuredSeconds)} · נזקף: {Duration(s.CreditedSeconds)}\nמשוב: {FeedbackName(s.Feedback)}\n{s.Notes}"));
   if(s.Feedback==null && run==null) box.Children.Add(Button("השלם משוב",()=>HistoricalFeedback(s)));
   page.Children.Add(new Expander{Header=s.Tag+" · "+s.Started.ToLocalTime().ToString("dd/MM HH:mm"),Content=box,Margin=new Thickness(5)});
  }
  if(steps.Count>60)page.Children.Add(Text("מוצגים 60 השלבים האחרונים; הייצוא כולל את כל ההיסטוריה."));
 }
 static string Duration(double seconds)=>$"{seconds/60:0.#} דקות";
 static string FeedbackName(string? value)=>value switch{"yes"=>"כן","half"=>"חצי־חצי","no"=>"לא",_=>"ממתין למשוב"};
 void Chart(List<(string label,double value)> values) {
  var panel=new StackPanel{Orientation=Orientation.Horizontal,FlowDirection=FlowDirection.LeftToRight};var max=Math.Max(1,values.Max(x=>x.value));
  foreach(var x in values) {
   var p=new StackPanel{Width=values.Count>15?27:66,VerticalAlignment=VerticalAlignment.Bottom,ToolTip=$"{x.label}: {Duration(x.value)}"};
   p.Children.Add(new Border{Height=Math.Max(2,90*x.value/max),Margin=new Thickness(3,0,3,0),Background=Brush(x.value>0?"#8873DC":"#DAD7E5"),CornerRadius=new CornerRadius(4)});
   p.Children.Add(new TextBlock{Text=x.label,FontSize=10,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,6,0,0)});panel.Children.Add(p);
  }
  page.Children.Add(new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(5,12,5,12)});
 }
 void Editor(List<Exercise>? preset=null) {
  Clear("בוא נבנה את המעגל שלך","בחר מה תרצה לתרגל וכמה דקות להקדיש לכל שלב.");rows.Clear();
  var recent=new WrapPanel();page.Children.Add(recent);
  foreach(var c in circuits) {var copy=c;recent.Children.Add(Button(string.Join(" · ",c.Select(x=>$"{x.Tag} {x.Minutes} דק׳")),()=>Editor(copy)));}
  var list=new StackPanel();page.Children.Add(list);
  void Add(Exercise exercise) {
   var box=new StackPanel{Margin=new Thickness(0,6,0,6)};
   var line=new WrapPanel();var tag=new TextBox{Text=exercise.Tag,Width=300,MaxLength=120};var min=new TextBox{Text=exercise.Minutes.ToString(),Width=90,FlowDirection=FlowDirection.LeftToRight};
   System.Windows.Automation.AutomationProperties.SetName(tag,"תגית האימון");System.Windows.Automation.AutomationProperties.SetName(min,"משך בדקות");
   line.Children.Add(tag);line.Children.Add(Text("דקות:"));line.Children.Add(min);line.Children.Add(Button("הסר שורה",()=>{if(rows.Count<=1)return;rows.Remove((tag,min));list.Children.Remove(box);}));box.Children.Add(line);
   var suggestions=new WrapPanel();foreach(var t in tags.Take(8)){var v=t;suggestions.Children.Add(Button(v,()=>tag.Text=v));}
   foreach(var m in minutes.Take(6)){var v=m;suggestions.Children.Add(Button(v.ToString(),()=>min.Text=v.ToString()));}box.Children.Add(suggestions);
   rows.Add((tag,min));list.Children.Add(box);
  }
  foreach(var x in preset??store.Get<List<Exercise>>("draft")??[new Exercise("אימון חופשי",15)])Add(x);
  page.Children.Add(Button("הוסף שורה",()=>{if(rows.Count<100)Add(new Exercise("",10));}));
  page.Children.Add(Button("התחל אימון",()=>{
   var c=rows.Select(x=>new Exercise(x.tag.Text.Trim(),int.TryParse(x.minutes.Text,out var m)?m:0)).ToList();Metrics.ValidateCircuit(c);
   store.Set("draft",c);circuits.RemoveAll(x=>x.SequenceEqual(c));circuits.Insert(0,c);circuits=circuits.Take(8).ToList();store.Set("circuits",circuits);
   tags=c.Select(x=>x.Tag).Concat(tags).Distinct().Take(12).ToList();minutes=c.Select(x=>x.Minutes).Concat(minutes).Distinct().Take(10).ToList();store.Set("tags",tags);store.Set("minutes",minutes);
   run=new RunState{Circuit=c};store.Save(run);StartStep();
  },"#BCEEDB"));page.Children.Add(Button("חזרה הביתה",Home));
 }
 void StartStep() {
  if(run==null)return;var item=run.Circuit[run.Index];
  run.Step=new PracticeStep{SessionId=run.SessionId,Tag=item.Tag,PlannedMinutes=item.Minutes,Started=DateTimeOffset.Now};run.Alerted=false;run.AwaitingFeedback=false;run.Paused=true;
  store.Save(run,run.Step);Resume();ShowRun();
 }
 void ShowRun() {
  if(run==null){Home();return;}
  if(run.Index>=run.Circuit.Count){Summary();return;}
  if(run.AwaitingFeedback){Feedback();return;}
  if(run.Step==null) {
   Clear("מוכנים להמשך?",$"שלב {run.Index+1} מתוך {run.Circuit.Count}");page.Children.Add(Button("התחל ‘"+run.Circuit[run.Index].Tag+"’",StartStep,"#BCEEDB"));page.Children.Add(Button("מסך הבית",Home));return;
  }
  Clear(run.Step.Tag,$"שלב {run.Index+1} מתוך {run.Circuit.Count} · תכנון: {run.Step.PlannedMinutes} דקות");
  timerText=Text("",100);timerText.FlowDirection=FlowDirection.LeftToRight;timerText.TextAlignment=TextAlignment.Center;page.Children.Add(timerText);
  statusText=Text("",21);statusText.TextAlignment=TextAlignment.Center;page.Children.Add(statusText);
  pauseButton=Button(run.Paused?"המשך לנגן":"השהה",()=>{if(run!.Paused)Resume();else Pause();ShowRun();});page.Children.Add(pauseButton);
  page.Children.Add(Button("סיים שלב",()=>{Pause();run!.Step!.Ended=DateTimeOffset.Now;run.AwaitingFeedback=true;store.Save(run,run.Step);Feedback();},"#BCEEDB"));
  page.Children.Add(Button("מסך הבית",Home));UpdateTimer();
 }
 void CommitSlice() {
  if(run?.Step==null||run.Paused||!clock.IsRunning)return;
  var elapsed=clock.Elapsed;clock.Restart();var end=sliceStart+elapsed;
  var previous=run.Step.Segments.LastOrDefault();
  if(previous!=null && Math.Abs((sliceStart-previous.End).TotalMilliseconds)<100)
   run.Step.Segments[^1]=new Segment(previous.Start,previous.End+elapsed);
  else run.Step.Segments.Add(new Segment(sliceStart,end));
  sliceStart=DateTimeOffset.Now;
 }
 void Pause() {if(run?.Step==null)return;CommitSlice();clock.Stop();run.Paused=true;store.Save(run,run.Step);UpdateTimer();}
 void Resume() {if(run?.Step==null||run.AwaitingFeedback)return;sliceStart=DateTimeOffset.Now;clock.Restart();run.Paused=false;store.Save(run,run.Step);}
 void Tick() {
  if(run?.Step==null||run.AwaitingFeedback||run.Paused)return;
  CommitSlice();
  var overdue=run.Step.MeasuredSeconds>=run.Step.PlannedMinutes*60;
  if(overdue&&!run.Alerted) {
   run.Alerted=true;store.Save(run,run.Step);SystemSounds.Exclamation.Play();
   try{new ToastContentBuilder().AddText("זמן לנגן — הזמן שהגדרת הסתיים").AddText(run.Step.Tag+" · אפשר להמשיך לנגן או לסיים את השלב.").Show();}
   catch(Exception ex){if(statusText!=null)statusText.Text="הזמן הסתיים. ההתראה לא הוצגה: "+ex.Message;}
  } else store.Save(run,run.Step);
  UpdateTimer();
 }
 void UpdateTimer() {
  if(run?.Step==null||timerText==null)return;
  var left=run.Step.PlannedMinutes*60-run.Step.MeasuredSeconds;
  timerText.Text=Metrics.Timer(left);timerText.Foreground=Brush(left<=0?"#C04A6E":"#7055C2");
  if(statusText!=null)statusText.Text=run.Paused?"האימון מושהה — הזמן הזה לא נספר":left<=0?"הזמן שהגדרת הסתיים. אפשר להמשיך בקצב שלך.":"יש לך מקום לנגן. אנחנו שומרים על הזמן.";
 }
 void Feedback() {
  if(run?.Step==null)return;Clear("איך היה השלב?","האם האימון בוצע?");
  FeedbackControls(run.Step,()=>{run.Index++;run.Step=null;run.AwaitingFeedback=false;run.Paused=true;store.Save(run);ShowRun();});
 }
 void FeedbackControls(PracticeStep step,Action done) {
  var choice=new WrapPanel();page.Children.Add(choice);var detail=new StackPanel();page.Children.Add(detail);
  void Choose(string value) {
   detail.Children.Clear();
   if(value=="no"){Save("");return;}
   detail.Children.Add(Text("על מה התאמנת?"));var notes=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=90,MaxLength=4000,Text=step.Notes};detail.Children.Add(notes);notes.TextChanged+=(s,e)=>{step.Notes=notes.Text;store.Save(run,step);};
   detail.Children.Add(Button("שמור והמשך",()=>Save(notes.Text.Trim()),"#BCEEDB"));
   void Save(string text){step.Feedback=value;step.Notes=text;store.Save(run,step);tools.FeedbackSaved(step);done();}
  }
  choice.Children.Add(Button("כן",()=>Choose("yes")));choice.Children.Add(Button("לא",()=>Choose("no")));choice.Children.Add(Button("חצי־חצי",()=>Choose("half")));
 }
 void HistoricalFeedback(PracticeStep step) {Clear("נשלים את התמונה",step.Tag+" · "+step.Started.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));FeedbackControls(step,Home);page.Children.Add(Button("חזרה",Home));}
 void Summary() {
  if(run==null)return;Clear("כל הכבוד — נתת מקום לנגינה!","הנה המעגל שסיימת, בקצב שלך.");
  var session=store.Steps().Where(s=>s.SessionId==run.SessionId).OrderBy(s=>s.Started).ToList();page.Children.Add(Text("זמן תרגול שנזקף: "+Duration(session.Sum(s=>s.CreditedSeconds)),24));
  foreach(var s in session)page.Children.Add(Text($"{s.Tag} · {Duration(s.CreditedSeconds)} · {FeedbackName(s.Feedback)}\n{s.Notes}"));
  page.Children.Add(Button("התחל מהתחלה",()=>{var c=run.Circuit;run=new RunState{Circuit=c};store.Save(run);StartStep();},"#BCEEDB"));
  page.Children.Add(Button("סיים",()=>{run=null;store.Save(null);store.Backup();Home();}));
 }
 void SessionSwitch(object sender,SessionSwitchEventArgs e) {if(e.Reason==SessionSwitchReason.SessionLock)Dispatcher.Invoke(()=>{tools.Suspend();Pause();if(timerText!=null)ShowRun();});}
 void PowerChanged(object sender,PowerModeChangedEventArgs e) {if(e.Mode==PowerModes.Suspend)Dispatcher.Invoke(()=>{tools.Suspend();Pause();if(timerText!=null)ShowRun();});}
 void Export() {
  var dialog=new SaveFileDialog{FileName="zman-lenagen-"+DateTime.Now.ToString("yyyy-MM-dd"),Filter="גיבוי מלא JSON|*.json|טבלת אימונים CSV|*.csv"};if(dialog.ShowDialog()!=true)return;
  var steps=store.Steps();string text;
  if(dialog.FilterIndex==1)text=JsonSerializer.Serialize(new ExportData(1,DateTimeOffset.Now,steps,circuits,tags,minutes,store.Get<List<Exercise>>("draft"),run),new JsonSerializerOptions{WriteIndented=true});
  else {
   static string Cell(string v)=>"\""+((v.StartsWith('=')||v.StartsWith('+')||v.StartsWith('-')||v.StartsWith('@')||v.StartsWith('\t')||v.StartsWith('\r'))?"'":"")+v.Replace("\"","\"\"")+"\"";
   text="מזהה שלב,מזהה סבב,תחילת אימון,סיום,תגית,דקות מתוכננות,שניות שנמדדו,שניות שנזקפו,משוב,על מה התאמנת\r\n"+string.Join("\r\n",steps.Select(s=>string.Join(",",new[]{s.Id,s.SessionId,s.Started.ToString("O"),s.Ended?.ToString("O")??"",s.Tag,s.PlannedMinutes.ToString(),s.MeasuredSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),s.CreditedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),FeedbackName(s.Feedback),s.Notes}.Select(Cell))));
  }
  var tmp=dialog.FileName+".tmp";File.WriteAllText(tmp,text,new UTF8Encoding(dialog.FilterIndex==2));File.Move(tmp,dialog.FileName,true);MessageBox.Show("הנתונים נשמרו. טוב שיש גיבוי.");
 }
 void Import() {
  if(run!=null){MessageBox.Show("סיים את המעגל השמור לפני ייבוא גיבוי.");return;}
  var dialog=new OpenFileDialog{Filter="גיבוי JSON|*.json"};if(dialog.ShowDialog()!=true)return;
  if(new FileInfo(dialog.FileName).Length>50*1024*1024)throw new ArgumentException("הקובץ גדול מדי (עד 50 MB).");
  var data=JsonSerializer.Deserialize<ExportData>(File.ReadAllText(dialog.FileName))??throw new ArgumentException("קובץ לא תקין.");
  store.Import(data);tags=store.Get<List<string>>("tags")??[];minutes=store.Get<List<int>>("minutes")??[];circuits=store.Get<List<List<Exercise>>>("circuits")??[];Home();MessageBox.Show("הגיבוי יובא. שלבים שכבר קיימים לא נוספו שוב.");
 }
 void Recordings() {Clear("ההקלטות שלך","רגעים מהנגינה, במקום אחד.");page.Children.Add(Button("חזרה הביתה",Home));tools.ShowArchive(page);}
 void Manage() {
  Clear("הנתונים שלך","ההיסטוריה נשמרת בחשבון Windows שלך. הסרת התוכנה משאירה אותה כדי שתוכל לחזור בעתיד.");
  page.Children.Add(Button("פתח תיקיית נתונים וגיבויים",()=>Process.Start(new ProcessStartInfo(Store.Folder){UseShellExecute=true})));
  page.Children.Add(Button("מחק את כל הנתונים וסגור",()=>{
   if(MessageBox.Show("למחוק לצמיתות את ההיסטוריה, המעגלים והגיבויים בחשבון זה? כדאי לייצא קודם. הפעולה אינה ניתנת לביטול.","מחיקת נתונים",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   Pause();tools.Dispose();heartbeat.Stop();store.Dispose();Directory.Delete(Store.Folder,true);ToastNotificationManagerCompat.Uninstall();closing=true;SystemEvents.SessionSwitch-=SessionSwitch;SystemEvents.PowerModeChanged-=PowerChanged;Close();
  },"#FFD5DF"));page.Children.Add(Button("חזרה",Home));
 }
}
