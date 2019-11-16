using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Input;

namespace CheggSolutionCollector
{
    internal class Program
    {
        private static Page _page;
        private static Page _headlessPage;

        public static string BookName = "Advanced-Engineering-Mathematics-10th-edition";
        public static string SolutionId = "9780470458365";

        public static string Url = "https://www.chegg.com/homework-help/" +
                                   $"{BookName}-" +
                                   "chapter-{CHAPTER}-" +
                                   "problem-{PROBLEM}-" +
                                   $"solution-{SolutionId}";

        public static string InitChapter = "1.R";
        public static Queue<Tuple<string, Queue>> Chapters = new Queue<Tuple<string, Queue>>();

        public static string InitProblem = "1P";
        public static Queue<string> Problems = new Queue<string>();

        private static readonly List<string> ImagesToLoad = new List<string>();

        private static TaskCompletionSource<Response> _responseTcs =
            new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static readonly EventHandler<ResponseCreatedEventArgs> ResponseHandler =
            delegate (object sender, ResponseCreatedEventArgs args)
            {
                if (args.Response.Url.EndsWith(".png") && ImagesToLoad.Contains(args.Response.Url))
                    ImagesToLoad.Remove(args.Response.Url);
                if (ImagesToLoad.Count == 0 && args.Response.Url.EndsWith(".png"))
                    _responseTcs.TrySetResult(args.Response);
            };

        private static readonly EventHandler<RequestEventArgs> RequestHandler =
            delegate (object sender, RequestEventArgs args)
            {
                if (args.Request.Url.EndsWith(".png"))
                    ImagesToLoad.Add(args.Request.Url);
            };

        private static void Main(string[] args)
        {
            CollectingTask().GetAwaiter().GetResult();
        }

        public static async Task CollectingTask()
        {
            //Initialize solution info
            Console.WriteLine("Enter book name:");
            BookName = Console.ReadLine();
            Console.WriteLine("Enter solution id:");
            SolutionId = Console.ReadLine();
            Console.WriteLine("Enter first chapter name:");
            InitChapter = Console.ReadLine();
            Console.WriteLine("Enter first problem name:");
            InitProblem = Console.ReadLine();

            Url = "https://www.chegg.com/homework-help/" +
                  $"{BookName}-" +
                  "chapter-{CHAPTER}-" +
                  "problem-{PROBLEM}-" +
                  $"solution-{SolutionId}";

            //Download browser
            Console.WriteLine("[!] Plz wait for browser to be ready...");
            var browserFetcherOptions = new BrowserFetcherOptions();
            var browserFetcher = new BrowserFetcher(browserFetcherOptions);
            await browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
            Console.WriteLine("[!] Browser is ready.");

            Browser browser = await Puppeteer.ConnectAsync(new ConnectOptions
            {
                BrowserURL = "http://127.0.0.1:9222"
            });
            Browser browserHeadless = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                UserDataDir = @"C:\CheggUserData"
            });

            //Get first open page of browser
            _page = (await browser.PagesAsync())[0];
            _headlessPage = (await browserHeadless.PagesAsync())[0];

            //Add network interception event
            _headlessPage.Request += RequestHandler;
            _headlessPage.Response += ResponseHandler;

            //Create book directory
            if (!Directory.Exists(BookName))
                Directory.CreateDirectory(BookName);

            //Initialize chapter
            var Problems = new Queue();
            Problems.Enqueue(InitProblem);
            var chapter = new Tuple<string, Queue>(InitChapter, new Queue(Problems));
            var problem = chapter.Item2.Dequeue().ToString();

            Reload:

            var url = Url.Replace("{CHAPTER}", chapter.Item1)
                .Replace("{PROBLEM}", problem);

            //Set cookies to go
            await _headlessPage.SetCookieAsync(await _page.GetCookiesAsync(url));

            await _headlessPage.GoToAsync(url, WaitUntilNavigation.Networkidle0);

            //Check title to handle recaptcha when shown
            var title = await _headlessPage.GetTitleAsync();
            if (title == "Access to this page has been denied.")
            {
                await _page.GoToAsync(url);
                Console.Write("[!] Solve captcha then press Enter...");
                Console.ReadLine();
                goto Reload;
            }

            //Add solution stylesheet to head
            //await headlessPage.EvaluateFunctionAsync("() => {" +
            //                                         "const linkStyle = document.createElement('link');" +
            //                                         "linkStyle.rel = 'stylesheet';" +
            //                                         "linkStyle.href = 'https://c.cheggcdn.com/chegg-solution-player/prod/26012928/css/styles.css';" +
            //                                         "document.head.appendChild(linkStyle);" +
            //                                         "}");

            //Wait to stylesheet applied
            //Thread.Sleep(1000);

            //Add watermark
            await _headlessPage.EvaluateFunctionAsync("() => {" +
                                                     "const mainElement = document.getElementsByClassName('main')[0];" +
                                                     "if(mainElement){" +
                                                     "const collectedByElement = document.createElement('p');" +
                                                     "collectedByElement.style='margin: 1.5rem 0 0 0;text-align: center;font-size: 16px;font-weight: 700;';" +
                                                     "collectedByElement.innerHTML='Collected by CE Khu 97';" +
                                                     "mainElement.appendChild(collectedByElement);" +
                                                     "}" +
                                                     "}");

            //Find toolbar trigger button
            var triggerButton = await _headlessPage.QuerySelectorAsync(".toggle-button-toolbar");
            await triggerButton.ClickAsync();

            //Crop solution
            await _headlessPage.EvaluateFunctionAsync("() => {" +
                                                     //Click on close button of accept cookie privacy
                                                     "const opt_box = document.getElementsByClassName('optanon-alert-box-close')[0];" +
                                                     "if(opt_box)" +
                                                     "opt_box.click();" +

                                                     //Disable any onload function
                                                     //"window.onload = function() {};" +

                                                     //Fix css style to prevent print solution
                                                     //"var style = document.createElement('style');" +
                                                     //"style.innerHTML = `.solution-player-steps li {display:list-item !important}" +
                                                     //".solution-player-steps:before{content:\"\" !important}`;" +
                                                     //"document.head.appendChild(style);" +

                                                     //Remove annoying element
                                                     "const relatedAnchors = document.getElementById('related-anchors');" +
                                                     "if(relatedAnchors)" +
                                                     "relatedAnchors.parentElement.removeChild(relatedAnchors);" +

                                                     //Remove review box
                                                     "const reviewBox = document.getElementsByClassName('review-box')[0];" +
                                                     "if(reviewBox)" +
                                                     "reviewBox.parentElement.removeChild(reviewBox);" +

                                                     //Move solution to body
                                                     //"const solutionPlayer = document.getElementById('solutionPlayer');" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[0]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[0]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     //"solutionPlayer.removeChild(solutionPlayer.children[6]);" +
                                                     "const solutionPlayer = document.getElementsByClassName('TBSPlayer')[0];" +
                                                     "document.body.appendChild(solutionPlayer);" +

                                                     //Remove unnecessary body child
                                                     //"document.body.removeChild(document.getElementsByClassName('chg-body')[0]);" +

                                                     //Hide unnecessary body child
                                                     "document.getElementsByClassName('chg-body')[0].style = 'display: none;';" +
                                                     "}");

            //Collect chapter and problem of book
            Console.WriteLine("[!] Collecting chapters started.");
            foreach (var chapterElement in await _headlessPage.QuerySelectorAllAsync("#chapter-current"))
            {
                var chapterName = (await (await chapterElement.GetPropertyAsync("innerHTML"))
                    .JsonValueAsync<string>()).Replace("Chapter ", "");

                //Skip existence chapters
                if (Directory.Exists($@"{BookName}\Chapter {chapterName}"))
                    continue;

                var parent = (await chapterElement.XPathAsync(".."))[0];
                var problemList = await parent.QuerySelectorAllAsync("li");
                while (problemList.Length == 0) { 
                    await chapterElement.ClickAsync();
                    problemList = await parent.QuerySelectorAllAsync("li");
                }
                var problems = new Queue();
                foreach (var problemElement in problemList)
                {
                    var problemName = await (await problemElement.GetPropertyAsync("innerHTML"))
                        .JsonValueAsync<string>();
                    problems.Enqueue(problemName);
                }

                Chapters.Enqueue(new Tuple<string, Queue>(chapterName, problems));
            }
            await triggerButton.ClickAsync();

            Console.WriteLine("[!] Collecting solutions started :D");
            while (Chapters.Count > 0)
            {
                chapter = Chapters.Dequeue();

                //Click on selected chapter item
                await triggerButton.ClickAsync();
                var h2Chapter = (await _headlessPage.XPathAsync($"//h2[text()='Chapter {chapter.Item1}']"))[0];
                await (await h2Chapter.XPathAsync(".."))[0].ClickAsync();
                await triggerButton.ClickAsync();

                while (chapter.Item2.Count > 0)
                {
                    problem = chapter.Item2.Dequeue().ToString();

                    //Click on selected problem item
                    await triggerButton.ClickAsync();
                    await (await _headlessPage.XPathAsync($"//li[text()='{problem}']"))[0].ClickAsync();
                    await triggerButton.ClickAsync();

                    //Add network interception event to find out all image are loaded
                    ImagesToLoad.Clear();
                    _headlessPage.Request += RequestHandler;
                    _responseTcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _headlessPage.Response += ResponseHandler;

                    //Wait until solution loaded
                    while ((await _headlessPage.QuerySelectorAllAsync(".step-num")).Length == 0)
                    {
                        if ((await _headlessPage.QuerySelectorAllAsync(".no-solution")).Length != 0)
                            break;
                        Thread.Sleep(1000);
                    }

                    try
                    {
                        //Wait until all images loaded
                        if ((await (await _headlessPage.QuerySelectorAsync(".TBS_SOLUTION"))
                                .QuerySelectorAllAsync("img")).Length > 0)
                            await await Task.WhenAny(_responseTcs.Task).WithTimeout(10000).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    //Remove network interception event
                    _headlessPage.Request -= RequestHandler;
                    _headlessPage.Response -= ResponseHandler;

                    //Create chapter directory
                    var chapterDirectoryName = $@"{BookName}\Chapter {chapter.Item1}";
                    if (!Directory.Exists(chapterDirectoryName))
                        Directory.CreateDirectory(chapterDirectoryName);
                    var savePath = Environment.CurrentDirectory
                                   + $@"\{chapterDirectoryName}\Problem {problem}.pdf";

                    //Save page as pdf
                    await _headlessPage.PdfAsync(savePath);
                    Console.WriteLine($"[+] {problem} of Chapter {chapter.Item1} Saved.");

                    //Click on next button
                    //await _headlessPage.EvaluateFunctionAsync("() => {" +
                    //                                         "const arrowRight = document.getElementsByClassName('arrow-right')[0];" +
                    //                                         "arrowRight.click();" +
                    //                                         "}");
                }
            }

            //Close Browser
            await browserHeadless.CloseAsync();
        }
    }
}