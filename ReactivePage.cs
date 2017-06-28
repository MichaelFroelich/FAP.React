/*
                GNU GENERAL PUBLIC LICENSE
                           Version 3, 29 June 2007
     Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
     Everyone is permitted to copy and distribute verbatim copies
     of this license document, but changing it is not allowed.
        Author: Michael J. Froelich
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAP
{
    /// <summary>
    /// Runs ReactJS along with a number of options, like transforming code and building a page if necessary.
    /// This class is not binary serializable but works perfectly fine from manual copying due to using the StaticParent instance 
    /// </summary>
    public class ReactivePage : Page
    {
        const int DEFAULTSEARCHDEPTH = 3;

        /// <summary>
        /// Fully instanstiated instance of FEngine for Transforming code:
        /// new ReactivePage.Engine.Transform(File.ReadAllText(pathnamehere));
        /// And for rendering HTML:
        /// new ReactivePage.Engine.RenderHtml(
        /// </summary>
        public static FEngine Engine { get; set; }

        /// <summary>
        /// A list of strings, manipulate if need be by Engine.BabelPresets instead
        /// </summary>
        public static List<object> BabelPresets => Engine.BabelPresets;

        /// <summary>
        /// A list of strings, manipulate if need be by Engine.BabelPlugins instead
        /// </summary>
        public static List<string> BabelPlugins => Engine.BabelPlugins;

        /// <summary>
        /// Parser options sent to Babylon, what's really transforming code. Set this by making it equal to an anonymous object, such as:
        /// ReactivePage.Engine.ParserOptions = new { allowImportExportEverywhere = true, allowReturnOutsideFunction = true };
        /// Which are the defaults, since you'd rather more code than less transforming. Alert me if plugins or presets cease working,
        /// it would be this line of code here. Set to null for the Babel/Babylon's true default.
        /// </summary>
        public static object BabylonParserOptions => Engine.ParserOptions;

        private static bool hasinit = false;

        private Component ccvars = null;

        private Component cvars
        {
            get {
                if (ccvars == null && StaticParent != null) ccvars = (StaticParent as ReactivePage).cvars;
                return ccvars; //This is the current optimal solution to the third scope, static, instantiated and readonly
            }
            set {
                ccvars = value;
            }
        }

        private Func<string, string, object> _get;

        /// <summary>
        /// Set a function which will be called when accessing this page through a "get" HTTP method. Return using
        /// Encoding.BigEndianUnicode for binary files (no warranties, no guarantees).
        /// </summary>
        /// <value>The get function</value>
        public Func<string, string, object> get
        {
            get {
                if (_get == null) _get = cvars.Get;
                return _get;
            }
            set {
                if (cvars.Get == null) cvars.Get = value;
                _get = value;
            }
        }

        /// <summary>
        /// Shortest interval in which new javascript has to be loaded, 5 seconds is good.
        /// </summary>
        /// <value>Time</value>
        public static int ScriptReloadTime { get; set; } = 500;

        /// <summary>
        /// Freely accessible default props. Will be overidden if props are returned from a get function.
        /// </summary>
        public object Props
        {
            get {
                return cvars.Props;
            }
            set {
                cvars.Props = value;
            }
        }

        /// <summary>
        /// Sets the page title such as < titl e>Title goes here< /title >
        /// </summary>
        public string Title
        {
            get {
                return cvars.Title.Substring(7, (cvars.Title.Length - 8) - 7);
            }
            set {
                cvars.Title = "<title>" + value + "</title>";
            }
        }

        private string componentName;

        private string ComponentName
        {
            get {
                if (componentName == null) {
                    componentName = cvars.Name;
                }
                return componentName;
            }
            set {
                componentName = value;
            }
        }

        /// <summary>
        /// Determines whether or not to generate HTML pre-amble, including script includes,
        /// or to generate just the component's HTML.
        /// </summary>
        public bool IsSPA
        {
            get {
                return cvars.IsSPA;
            }
            set {
                cvars.IsSPA = value;
            }
        }

        /// <summary>
        /// Set <c>true</c> to include the jQuery library as a hosted script in the resultant HTML from this page. Default is <c>false</c>.
        /// </summary>
        public bool IncludeJQuery
        {
            get {
                return !String.IsNullOrEmpty(cvars.Scripts[3]);
            }
            set {
                var s = cvars.Scripts;
                if (!value) {
                    s[3] = String.Empty;
                }
                if (value) {
                    s[3] = Component.JQUERY;
                    if (!Engine.ReactScriptPaths.Contains("mock") && !Engine.ReactScriptPaths.Contains("jquery")) { //two plausible things you might name a file that allows jquery code but isn't necessarily jquery
                        string path = Search("jquery-mock"); //Do not download jquery-mock if the user already has it
                        if (path == null) {
                            path = DownloadScript(Component.MOCKJQUERY); //A last resort
                        }
                        IncludeMockScript(path);
                    }
                }
            }
        }

        internal static string DownloadScript(string what)
        {
            string path;
            byte[] script = null;
            if (JsFolder == Directory.GetCurrentDirectory().ToString())
                if (Engine != null && Engine.ReactScriptPaths.Count > 0) { //I'd rather use whatever react is using
                    path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Engine.ReactScriptPaths[0]), what.Substring(what.LastIndexOf('/') + 1));
                    JsFolder = System.IO.Path.GetDirectoryName(Engine.ReactScriptPaths[0]);
                }
                else {
                    string directory = System.IO.Path.Combine(JsFolder, "js");
                    path = System.IO.Path.Combine(directory, what.Substring(what.LastIndexOf('/') + 1));
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                }
            else
                path = System.IO.Path.Combine(JsFolder, what.Substring(what.LastIndexOf('/') + 1));
            try { script = new System.Net.WebClient().DownloadData(what); }
            catch (Exception e) {
                Console.WriteLine("15: Error downloading script " + what + " with a message of :\n" + e.Message);
            }
            if (script != null)
                File.WriteAllBytes(path, script);
            return path;
        }

        /// <summary>
        /// Set false to not include the Babel Script library as a hosted script in the resultant HTML from this page. Also enable the script type as babel for client side transpiling.
        /// </summary>
        public bool IncludeBable
        {
            get {
                return !String.IsNullOrEmpty(cvars.Scripts[2]);
            }
            set {
                var s = cvars.Scripts;
                if (!value) {
                    s[2] = String.Empty;
                }
                if (value) {
                    s[2] = Component.BABEL;
                }
            }
        }

        /// <summary>
        /// Set <c>false</c> to not include the react libraries and babel as a hosted scripts when using the SPA.  Default is <c>true</c>.
        /// Setting this to false shall also disable React serverside rendering additions, the ugly stuff. This is a good idea if IsSPA is false.
        /// </summary>
        public bool IncludeReact
        {
            get {
                return !String.IsNullOrEmpty(cvars.Scripts[0]);
            }
            set {
                var s = cvars.Scripts;
                if (!value) {
                    s[0] = String.Empty;
                    s[1] = String.Empty;
                }
                if (value) {
                    s[0] = Component.REACT;
                    s[1] = Component.REACTDOM;
                }
            }
        }

        /// <summary>
        /// Gets or sets the ECMA stage. This is passed into Babel as a preset of "stage-*"
        /// 0 uses ECMA script proposal features, there are more proposals.
        /// 3 uses ECMA script candidate features, there are fewer candidates.
        /// </summary>
        /// <value>Stage, 0 to 3</value>
        public static int EcmaStage
        {
            get {
                string old = Engine.BabelPresets.Where(s => (s is string) && (s as string).StartsWith("stage-")).FirstOrDefault() as string;
                if (old == null) return -1;
                int ecmaStage;
                if (int.TryParse(old.Substring("stage-".Length, 1), out ecmaStage))
                    return ecmaStage;
                return 2;
            }
            set {
                if (value >= 0 && value <= 3) {
                    if (hasinit) {
                        string old = Engine.BabelPresets.Where(s => (s is string) && (s as string).StartsWith("stage-")).FirstOrDefault() as string;
                        if (old == null) return;
                        Engine.BabelPresets.Remove(old);
                        Engine.BabelPresets.Add(("stage-" + value.ToString()));
                    }
                }
            }
        }

        private static int ecmaYear = 2015;
        //public static ISet<string> BabelPresets => ReactSiteConfiguration.Configuration.BabelConfig.Presets;
        //public static ISet<string> BabelPlugins => ReactSiteConfiguration.Configuration.BabelConfig.Plugins;

        public static int EcmaYear
        {
            get {
                string old = Engine.BabelPresets.Where(s => (s is string) && (s as string).StartsWith("es20")).FirstOrDefault() as string;
                int year;
                if (int.TryParse(old.Substring(2, 4), out year))
                    return year;
                return ecmaYear; //Kept in case
            }
            set {
                if (value >= 2015 && value <= 2017) {
                    if (hasinit) {
                        //ReactSiteConfiguration.Configuration.BabelConfig.Presets.Remove("es" + ecmaYear + "-no-commonjs");
                        string old = Engine.BabelPresets.Where(s => (s is string) && (s as string).StartsWith("es20")).FirstOrDefault() as string;
                        string rest = old.Substring(6, old.Length - 6);
                        if (!string.IsNullOrEmpty(rest) && !string.IsNullOrEmpty(old)) {
                            Engine.BabelPresets.Remove(old);
                            //ReactSiteConfiguration.Configuration.BabelConfig.Presets.Add("es" + value + "-no-commonjs");
                            Engine.BabelPresets.Add("es" + value + rest);
                            ecmaYear = value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize this instance.
        /// </summary>
        internal static void Initialize()
        {
            if (hasinit)
                return;
            Engine = new FEngine(); //This is all I need now.
                                    //Initializer.Initialize(registration => registration.AsSingleton());
                                    //container = React.AssemblyRegistration.Container;
                                    // Register some components that are normally provided by the integration library
                                    // (eg. React.AspNet or React.Web.Mvc6)
                                    //container.Register<ICache, NullCache>();
                                    //container.Register<IFileSystem, SimpleFileSystem>();
                                    //EcmaStage = 0;//Here at FAP we like to live dangerously
                                    //environment = ReactEnvironment.Current;
                                    //defaults = new Dictionary<string, Tuple<object, string>>();
            hasinit = true;
        }

        /// <summary>
        /// If given a valid path to a local file, will include the CSS as an internal style sheet
        /// Otherwise, any string given will be considered a script source
        /// </summary>
        /// <param name="Pathname">Path or URL to the CSS file (ie yourwebsite.com/css/main.css)</param>
        /// <returns></returns>
        public void IncludeCSS(string Pathname)
        {
            string style = null;
            if (File.Exists(Pathname))
                style += "\t\t\t<style>\n" + File.ReadAllText(Pathname) + "\t\t\t</style>\n";
            else
                style += "\t\t\t<link rel=\"stylesheet\" href=\"" + Pathname + "\">\n";
            if (!cvars.Style.Contains(style)) {
                cvars.Style += style;
            }
        }

        /// <summary>
        /// If given a valid path to a local file, will include the CSS as an internal style sheet
        /// Otherwise, any string given will be considered a script source
        /// </summary>
        /// <param name="Pathname">Path or URL to the CSS file (ie yourwebsite.com/css/main.css)</param>
        /// <returns></returns>
        public void IncludeCSS(IEnumerable<string> Paths)
        {
            foreach (string s in Paths)
                IncludeCSS(s);
        }

        /// <summary>
        /// Add HTML metadata typically found in the head of the HTML file for a single page application.
        /// For example: with meta = theme-color and content = #ff0000, the browser will appear red for mobile clients
        /// </summary>
        /// <param name="meta">The meta data name label (description, theme-color, viewport etc)</param>
        /// <param name="content">The content of that meta data</param>
        public void AddMeta(string meta, string content)
        {
            string metad = "<meta name=\"" + meta + "\" content=\"" + content + "\">";
            if (!cvars.Metadata.Contains(metad))
                cvars.Metadata += metad;
        }

        /// <summary>
        /// As the AddMeta, adds a charset (such as utf-8) to a single page application
        /// This will not change FAP's charset
        /// </summary>
        /// <param name="charset"></param>
        public void Charset(string charset)
        {
            string metad = "<meta charset=\"" + charset + "\">";
            if (!cvars.Metadata.Contains(metad))
                cvars.Metadata += metad;
        }

        /// <summary>
        /// Adds scripts for both the ReactJS engine as well as to be included within script tags on the resultant page.
        /// If given a valid URL to "Pathname" or if useRenderMachine is false, this method will create script tags with a source property of your URL.
        /// Otherwise, if useRenderMachine is false it will search the file system. You may need to include it again if IsSPA is true.
        /// Please remember: the order that you include scripts absolutely does matter.
        /// </summary>
        /// <param name="Pathname">The pathname, beginning from the binary, to the JS/JSX script file to be included.</param>
        /// <param name="useRenderMachine">True for React component script local pathnames, false for external libraries which will be linked as script sources for your SPA
        /// Basically, if useRenderMachine = false then this line: cvars.Scripts.Add("<script src='" + Pathname + "'></script>");</param>
        /// <param name="Type">Used if useRenderMachine is false, changes the type of <script type="text/TypeHere</param>
        /// <returns></returns>
        public void IncludeScript(string Pathname, bool useRenderMachine = true, string Type = null)
        {
            if (!hasinit)
                Initialize();
            Script s = new Script();
            s.isJSX = Pathname.EndsWith(".tsx") || Pathname.EndsWith(".jsx");
            if (!useRenderMachine) {
                string toadd = null;
                if (IsURL(Pathname)) {
                    if (s.isJSX) {
                        toadd = ("\n\t\t<script type=\"text/babel\" src='" + Pathname + "'></script>");
                    }
                    else if (Type != null)
                        toadd = (string.Format("\n\t\t<script type=\"text/{0}\"  src=\"{1}\"></script>", Type, Pathname));
                    else {
                        toadd = ("\n\t\t<script src='" + Pathname + "'></script>");
                    }
                }
                else if (File.Exists(Pathname)) {
                    string actualscript = File.ReadAllText(Pathname);
                    if (s.isJSX)
                        actualscript = Engine.TransformCode(actualscript);
                    toadd = (string.Format("\n\t\t<script {0}>\n{1}\n\t\t</script>", Type == null ? string.Empty : "type=\"text/" + Type + "\"", actualscript));
                }
                else {
                    string realpath = Search(Pathname);
                    if (realpath == null)
                        return;
                    string actualscript = File.ReadAllText(realpath);
                    if (istransformable(realpath))
                        actualscript = Engine.TransformCode(actualscript);
                    toadd = (string.Format("\n\t\t<script {0}>\n{1}\n\t\t</script>", Type == null ? string.Empty : "type=\"text/" + Type + "\"", actualscript));
                }
                if (toadd != null && !cvars.Scripts.Contains(toadd))
                    cvars.Scripts.Add(toadd);
            }
            else {
                try {
                    string foundpath = Search(Pathname);
                    if (foundpath != null)
                        if (!cvars.ComponentScriptPathinfo.ContainsKey(foundpath)) {
                            loadScriptFile(s, foundpath);
                            cvars.ComponentScriptPathinfo.Add(foundpath, s);
                        }
                }
                catch (Exception e) {
                    Console.Error.WriteLine("10: FAP.React script include error: \n" + e.Message);
                }
            }
        }

        /// <summary>
        /// Creates an array of reactive pages based on the components found within scripts of the specified folder.
        /// Only the first component found will be included. On failure, it will continually check a layer deeper
        /// from the specified folder
        /// </summary>
        /// <param name="JavascriptFolder">Folder to source  scripts from</param>
        /// <returns>A list of ReactivePages with any found components</returns>
        public static List<ReactivePage> CreateSite(string JavascriptFolder) => CreateSite(new[] { JavascriptFolder });

        /// <summary>
        /// Creates an array of reactive pages based on the components found within scripts of the specified folder.
        /// Only the first component found will be included. On failure, it will continually check a layer deeper
        /// from the specified folder until no directory exists. This function reminds you function begins with fun!
        /// </summary>
        /// <param name="JavascriptFolder">Folder to source  scripts from</param>
        /// <returns>A list of ReactivePages with any found components</returns>
        public static List<ReactivePage> CreateSite(IEnumerable<string> JavascriptFolder = null)
        {
            string[] jfolder;
            List<ReactivePage> pages = new List<ReactivePage>();
            if (JavascriptFolder == null)
                jfolder = new[] { JsFolder };
            else
                jfolder = JavascriptFolder.ToArray();
            foreach (string s in jfolder) {
                if (Directory.Exists(s)) {
                    var potentialFiles = Directory.GetFiles(s, "*", SearchOption.AllDirectories)
                        .Select(System.IO.Path.GetFullPath)
                        .Where(isscript)
                        .Where(n => new FileInfo(n).Length > 16)
                        .ToList();
                    foreach (string files in potentialFiles) {
                        int renderfunctionfound = -1;
                        string componentNameArea;
                        var file = File.ReadAllText(files);
                        bool repeat = true;
                        string[] scriptLines;
                        while (repeat) {
                            repeat = false; //only do this again if it finds a Component
                            if ((renderfunctionfound = file.IndexOf("render()")) > 0 ||
                                (renderfunctionfound = file.IndexOf(".render =")) > 0 ||
                                (renderfunctionfound = file.IndexOf(".render=")) > 0 ||
                                (renderfunctionfound = file.IndexOf("render:")) > 0) {
                                componentNameArea = file.Substring(0, renderfunctionfound);
                                scriptLines = componentNameArea.Split('\n');
                                int removethis = -1;
                                string componentname = null;
                                for (int i = scriptLines.Length - 1; i >= 0 && string.IsNullOrEmpty(componentname); i--) {
                                    if (char.IsLetter(scriptLines[i][0])) { //Forced minimal indentation. Sorry!
                                        componentname = scriptLines[i];
                                        while (removethis < 0)
                                            switch (componentname[0]) {
                                                case ('c'): //class //const
                                                    if (componentname.StartsWith("const"))
                                                        componentname = componentname.Substring(5, componentname.Length - 5).TrimStart();
                                                    else if (componentname.StartsWith("class"))
                                                        removethis = 5;
                                                    break;

                                                case ('l'):
                                                case ('v'): //
                                                    if (componentname.StartsWith("let") || componentname.StartsWith("var"))
                                                        removethis = 3;
                                                    break;

                                                case ('f'): //function
                                                    if (componentname.StartsWith("function"))
                                                        removethis = 8;
                                                    break;

                                                case ('e'): //oh no.. export
                                                    if (componentname.StartsWith("export"))
                                                        componentname = componentname.Substring(6, componentname.Length - 6).TrimStart();
                                                    break;

                                                default:
                                                    if (scriptLines[i].Contains("=")) {
                                                        removethis = 0;
                                                    }
                                                    break;
                                            }
                                        if (removethis < 0 && char.IsLetter(componentname[0]) && scriptLines[i].Contains("="))
                                            removethis = 0;

                                        componentname = componentname.Substring(removethis, componentname.Length - removethis);
                                        if (((removethis = componentname.IndexOf(" extends ")) > 0) ||
                                            ((removethis = componentname.IndexOf("=")) > 0) ||
                                            ((removethis = componentname.IndexOf("(")) > 0)) {
                                            componentname = componentname.Substring(0, removethis).Trim();
                                        }
                                    }
                                }
                                if (componentname != null && componentname.All(c => char.IsLetterOrDigit(c) || c == '_')) {
                                    var nrp = new ReactivePage(componentname);
                                    nrp.IncludeScript(files);
                                    pages.Add(nrp);
                                    file = file.Substring(componentNameArea.Length);
                                    repeat = true;
                                    renderfunctionfound = -1;
                                }
                            }
                        }
                    }
                }
            }
            if (pages.Count <= 0) {
                string newpath = System.IO.Path.Combine(jfolder[0], "../");
                if (Directory.Exists(newpath))
                    return CreateSite(newpath);
            }
            return pages;
        }

        /// <summary>
        /// Adds scripts for both the ReactJS engine as well as to be included within script tags on the resultant page.
        /// If given a valid URL to "Pathname" or if useRenderMachine is false, this method will create script tags with a source property of your URL.
        /// Otherwise, if useRenderMachine is false it will search the file system. You may need to include it again if IsSPA is true.
        /// Please remember: the order that you include scripts absolutely does matter.
        /// </summary>
        /// <param name="Pathnames">Enumerable of paths</param>
        /// <param name="useRenderMachine">If set to <c>true</c> use render machine.</param>
        public void IncludeScript(IEnumerable<string> Pathnames, bool useRenderMachine = true)
        {
            foreach (string s in Pathnames)
                IncludeScript(s, useRenderMachine);
        }

        /// <summary>
        /// Adds the react script paths found used by the engine to the internally sourced, unrendered scripts.
        /// This also disables sourced scripts from the unpkg CDN from the output page and so adds slightly to average load speeds.
        /// Please note, this is only advantageous if you expect your server to perform a lot of 304s and is otherwise a bad idea.
        /// </summary>
        public void IncludeReactAsInternalScripts()
        {
            var s = Engine.ReactScriptPaths;
            for (int i = 0; i < 2; i++) {
                if (s[i].Contains("-server")) {
                    var path = s[i].Remove(s[i].LastIndexOf("-server"), "-server".Length);
                    if (File.Exists(path))
                        IncludeScript(path, false);
                    else {
                        throw new Exception("Could not find react-dom, suggest running ReactivePage.DownloadScripts()");
                    }
                }
                else
                    IncludeScript(Search(s[i]), false); //I prefer this over relying on the order of a list
            }
            cvars.InternalReact = true; //No going back from this
        }

        /// <summary>
        /// Used to execute scripts for the render machine before for component files, his will not be included in the SPA output.
        /// These scripts will only ever run serverside. They are a step above typings or source map which may be included normally.
        /// For deployment, add the scripts here and add them again with the useRenderMachine as false to decrease initial load.
        /// This will run the script upon instantiating the JS engine but will only run the render function upon a connection.
        /// </summary>
        /// <param name="Pathname">Path to the script in question</param>
        /// <param name="UseRenderMachine">Whether or not to transform the code beforehand</param>
        /// <param name="haveIncludedSourcemaps">Set <c>true</c> if you've included a sourcemap script</param>
        public void IncludeMockScript(string Pathname, bool UseRenderMachine = false, bool haveIncludedSourcemaps = false)
        {
            if (istransformable(Pathname) || UseRenderMachine) {
                //ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScript(Pathname);
                Engine.IncludeScript(Pathname, true, FEngine.Machine.React);  //it needs both for rendering, which is a definite,
                if (!haveIncludedSourcemaps)
                    Engine.IncludeScript(Pathname, true, FEngine.Machine.Babel);  //and transformation, or you won't pass babel validation
            }
            else {
                //ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScriptWithoutTransform(Pathname);
                Engine.IncludeScript(Pathname, false, FEngine.Machine.React);
                if (!haveIncludedSourcemaps)
                    Engine.IncludeScript(Pathname, false, FEngine.Machine.Babel);
            }
        }

        /// <summary>
        /// Used to execute scripts for the render machine before for component files, his will not be included in the SPA output.
        /// These scripts will only ever run serverside. They are a step above typings or source map which may be included normally.
        /// For deployment, add the scripts here and add them again with the useRenderMachine as false to decrease initial load.
        /// This will run the script upon instantiating the JS engine but will only run the render function upon a connection.
        /// </summary>
        /// <param name="Pathnames">Pathnames.</param>
        /// <param name="UseRenderMachine">If set to <c>true</c> use render machine.</param>
        /// <param name="haveIncludedSourcemaps">Set <c>true</c> if you've included a sourcemap script</param>
        public void IncludeMockScript(IEnumerable<string> Pathnames, bool UseRenderMachine = false, bool haveIncludedSourcemaps = false)
        {
            foreach (string s in Pathnames)
                IncludeMockScript(s, UseRenderMachine, haveIncludedSourcemaps);
        }

        /// <summary>
        /// Incudes a script into the babel engine to allow validation when transforming JSX to js
        /// Use this if you're encountering undefined errors, you must still include the real script normally.
        /// Synonymous to Engine.IncludeScript(Pathname, false, FEngine.Machine.Babel);
        /// </summary>
        /// <param name="Pathname"></param>
        public void IncludeSourceMap(string Pathname, bool UseRenderMachine = false)
        {
            if (istransformable(Pathname) || UseRenderMachine) {
                Engine.IncludeScript(Pathname, true, FEngine.Machine.Babel);  //and transformation, or you won't pass babel validation
            }
            else {
                Engine.IncludeScript(Pathname, false, FEngine.Machine.Babel);
            }
        }

        /// <summary>
        /// Incude scripts into the babel transformation engine to allow validation when transforming JSX to js
        /// Use this if you're encountering undefined errors, you must still include the real scripts normally.
        /// </summary>
        /// <param name="Pathname"></param>
        public void IncludeSourceMap(IEnumerable<string> Pathname, bool UseRenderMachine = false)
        {
            foreach (string s in Pathname)
                IncludeSourceMap(s, UseRenderMachine);
        }

        /// <summary>
        /// Includes one script as an internal script, either hosted or from file, and the other as a script for the internal engine.
        /// This is useful for popular scripts that break v8 engines without a document or DOM set up, like jQuery.
        /// </summary>
        /// <param name="OriginalScript">A path, ie either "jquery.js" or http://scripts.com/jquery.js</param>
        /// <param name="TypingOrMock">A local path to a substitute script that as a minimum allows validation</param>
        public void IncludeBrokenScript(string OriginalScript, string TypingOrMock)
        {
            IncludeScript(OriginalScript, false);
            IncludeMockScript(TypingOrMock);
        }

        /// <summary>
        /// Includes one script as an internal script, either hosted or from file, and the other as a script for the internal engine.
        /// This is useful for popular scripts that break v8 engines without a document or DOM set up, like jQuery.
        /// </summary>
        /// <param name="OriginalScript">A path, ie either "jquery.js" or http://scripts.com/jquery.js</param>
        /// <param name="TypingOrMock">A local path to a substitute script that as a minimum allows validation</param>
        public void IncludeBrokenScript(IEnumerable<string> OriginalScript, IEnumerable<string> TypingOrMock)
        {
            foreach (string s in OriginalScript)
                IncludeScript(s, false);
            foreach (string s in TypingOrMock)
                IncludeMockScript(s);
        }

        private static void loadScriptFile(Script s, string scriptFullPath)
        {
            s.ComponentScript = File.ReadAllText(scriptFullPath);
            if (s.isJSX)
                s.RenderedComponentScript = Engine.TransformCode(s.ComponentScript, System.IO.Path.GetFileName(scriptFullPath));
            //s.RenderedComponentScript = ReactEnvironment.Current.Babel.Transform(s.ComponentScript);
            s.Size = new FileInfo(scriptFullPath).Length;
            s.ScriptPath = scriptFullPath;
        }

        /// <summary>
        /// This should be called by FAP when serving and not by the user
        /// </summary>
        public ReactivePage()
        {
        }

        /// <summary>
        /// A simplified constructor that assumes the path is the component name as all lower case.
        /// BEFORE calling this constructor, please ensure the react, react-dom-server and babel scripts are somewhere around the binary.
        /// Please avoid capitalisation.
        /// </summary>
        /// <param name="PathAndComponentName">Path and component name.</param>
        /// <param name="initialProps">The initial properties passed to the React renderer, it can otherwise be changed via the "Props" property or by assigning a "get" (lowercase) function and returning a JSON string from it</param>
        public ReactivePage(string PathAndComponentName, object initialProps = null)
            : this(PathAndComponentName.ToLower(), PathAndComponentName, initialProps)
        {
        }

        /// <summary>
        /// This may be called at startup for each component that may be served via FAP
        /// BEFORE calling this constructor, please ensure the react, react-dom-server and babel scripts are somewhere around the binary
        /// Please avoid capitalisation.
        /// </summary>
        /// <param name="path">The path to this page, ie 127.0.0.1/path?</param>
        /// <param name="componentName">The component name as found in JSX files</param>
        /// <param name="initialProps">The initial properties passed to the React renderer, it can otherwise be changed via the "Props" property or by assigning a "get" (lowercase) function and returning a JSON string from it</param>
        public ReactivePage(string path, string componentName, object initialProps)
            : base(path) //even here we must call the base constructor
        {   //For overridden classes: public public ReactivePage(...
            //                                          : base(string p, string c, object i)
            Initialize();
            ComponentName = componentName;
            ccvars = new Component {
                Path = path,
                Props = initialProps,
                ComponentScriptPathinfo = new Dictionary<string, Script>(),
                Name = componentName
            };
            Component.ComponentRegistry.Add(Path, ccvars);
        }

        private object oldprops;
        private string oldtext;

        /// <summary>
        /// Get function, called by FAP internals, not to be called or overidden by users. This will call the get (lowercase) function and deserialise its output for the props object.
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="messageContent"></param>
        /// <returns></returns>
        public sealed override string Get(string queryString, string messageContent)
        {
            object props = null;
            string sprops = null;
            try {
                if (base.get != null) {
                    sprops = base.get(queryString, messageContent);
                    props = JsonConvert.DeserializeObject<object>(sprops);
                }
                else if (this.get != null || cvars.Get != null) {
                    if (get == null) get = cvars.Get; //initialise the get if uninitialised. No. FAP will not do this for you.
                    props = get(queryString, messageContent);
                    if (props is string) {
                        string propsstring = props as string;
                        if ((propsstring[0] == '{' && propsstring[propsstring.Length - 1] == '}') ||
                            (propsstring[0] == '[' && propsstring[propsstring.Length - 1] == ']') ||
                            (propsstring[0] == '"' && propsstring[propsstring.Length - 1] == '"')) {
                            props = JsonConvert.DeserializeObject<object>(propsstring);
                            sprops = propsstring; //I still need it in both object and JSON form for comparison and usage respectively
                        }
                    }
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine("11: Json.NET Parse error: " + e.Message);
            }
            //Do not run the rendering machine if the props are unchanged.
            if (props == null) {
                props = Props;
                if (props == null) {
                    props = string.Empty;       //if the props are still empty, become propsless component
                    oldprops = string.Empty;    //This is needed to as these additions are for JToken
                }
            }
            if (oldprops == null || oldtext == null || cvars.Changed ||
                !JToken.DeepEquals(JToken.FromObject(props), JToken.FromObject(oldprops))) {
                cvars.Changed = false; //FYI this feature will not work perfectly on a live system, not that anyone would ever do that.. right?
                oldprops = props;
                try {
                    if (sprops == null)
                        if (props == null || (props as string) == string.Empty)
                            sprops = "null";
                        else
                            JsonConvert.SerializeObject(props);
                    string html;
                    html = Engine.RenderHtml(ComponentName, sprops, !IncludeReact, cvars);
                    if (IsSPA) 
                        oldtext = SpaBuilder(IncludeReact, html, sprops, cvars);
                    else
                        oldtext = html;
                }
                catch (Exception e) {
                    Console.Error.WriteLine("12: ReactJS.NET render error: " + e.Message +
                        (e.Message.Contains("Unable to resolve type:") ? "\n\tYou may need to update/upgrade your system" : ""));
                    return "502\r\n" + e.Message + "\r\n\n" + e.StackTrace;
                }
            }
            return oldtext;
        }
        /// <summary>
        /// Used for building SPAs, entire webpages returned as strings
        /// </summary>
        /// <param name="IncludeReact">Whether or not to run the ReactDOM.render function</param>
        /// <param name="html">Child HTML</param>
        /// <param name="sprops">Properties already serialised</param>
        /// <param name="cvars">All the properties/things I found warranted the mediant scope such as page title, styles and meta data</param>
        /// <returns></returns>
        internal static string SpaBuilder(bool IncludeReact, string html, string sprops, Component cvars)
        {
            var output = new StringBuilder(Component.OPENINGHEADER).Append(cvars.Title).Append(cvars.Metadata).Append(cvars.Style).Append(Component.CLOSINGHEADER).Append(html);
            int i = cvars.InternalReact ? 2 : 0; //Horrible hack all because I want more options available via changing boolean properties
            for (; i < cvars.Scripts.Count; i++) {
                if (!string.IsNullOrEmpty(cvars.Scripts[i]))
                    output.Append(cvars.Scripts[i]);
            }
            bool hasBabel = cvars.Scripts != null && cvars.Scripts.Count > 2 && cvars.Scripts.Any(s => (s.TrimStart('\n','\t').StartsWith("<script src=") && s.Contains("babel.")));
            if(cvars.ComponentScriptPathinfo != null)
            foreach (Script s in cvars.ComponentScriptPathinfo.Values) {
                if (hasBabel && s.isJSX) {
                    output.Append(Component.BabelType).Append("\n");
                }
                else {
                    output.Append(Component.RegularType).Append("\n");
                }
                if (s.isJSX && !hasBabel) {
                    if (string.IsNullOrEmpty(s.RenderedComponentScript))
                        s.RenderedComponentScript = Engine.TransformCode(s.ComponentScript);//ReactEnvironment.Current.Babel.Transform(s.ComponentScript);
                    output.Append(s.RenderedComponentScript);
                }
                else {
                    output.Append(s.ComponentScript);
                }
                output.Append("\n\t\t</script>");
            }
            if (IncludeReact)
                output.Append(Component.RegularType).Append(Engine.RenderJavaScript(cvars.Name, sprops)).Append("</script>\n");
            return output.Append(Component.FOOTER).ToString();
        }

        internal static bool IsURL(string source)
        {
            Uri uriResult;
            return Uri.TryCreate(source, UriKind.Absolute, out uriResult) && ((uriResult.Scheme == Uri.UriSchemeHttp) || (uriResult.Scheme == Uri.UriSchemeHttps));
        }

        /// <summary>
        /// Downloads scripts needed for running a Reactive page, this can lead to duplicate scripts if you do not set a JsPath
        /// </summary>
        /// <param name="JsPath">It's usually a good idea to set a path to download the scripts to</param>
        public static void DownloadScripts(string JsPath = null)
        {
            if (JsPath != null)
                JsFolder = JsPath;
            if (Search("react.min.js") == null)
                DownloadScript("https://unpkg.com/react@latest/dist/react.min.js");
            if (Search("react-dom-server.min.js") == null)
                DownloadScript("https://unpkg.com/react-dom@latest/dist/react-dom-server.min.js");
            if (Search("react-dom.min.js") == null)
                DownloadScript("https://unpkg.com/react-dom@latest/dist/react-dom.min.js");
            if (Search("babel.min.js") == null)
                DownloadScript("https://unpkg.com/babel-standalone@latest/babel.min.js");
        }

        /// <summary>
        /// Private class used as a sort of smart structure
        /// </summary>
        internal class Component
        {
            private bool isrunning = false;
            internal static Dictionary<string, Component> ComponentRegistry { get; set; } = new Dictionary<string, Component>();
            public Component(bool register = true)
            {
                if(register)
                    Task.Factory.StartNew(scriptRegister);
            }

            private async void scriptRegister()
            {
                await Task.Delay(ScriptReloadTime);
                if (ComponentRegistry.ContainsKey(Path) && ComponentRegistry[Path].isrunning == true) //Am I a clone?
                    return;
                try {
                    isrunning = true; //this works because the inescapable loop is after this point, but I must have the Path
                    while (ComponentRegistry[Path] != null) {
                        isrunning = false;
                        if (ComponentScriptPathinfo != null && ComponentScriptPathinfo.Count > 0) {
                            foreach (Script s in ComponentScriptPathinfo.Values) {
                                if (!string.IsNullOrEmpty(s.ScriptPath)) {
                                    FileInfo f = new FileInfo(s.ScriptPath);
                                    if (f.Exists && f.Length != s.Size) {
                                        Changed = true;
                                        loadScriptFile(s, s.ScriptPath); //It's best to do this one at a time as you'd only work on one file at a time
                                    }
                                    else if (!f.Exists) {
                                        ComponentScriptPathinfo.Remove(s.ScriptPath);
                                    }
                                }
                            }
                        }
                        isrunning = true;
                        await Task.Delay(ScriptReloadTime);
                    }
                }
                catch (Exception e) {
                    Console.Error.WriteLine("13: " + DateTime.UtcNow + " Script Load Error: \n" + e.Message);
                    await Task.Delay(ScriptReloadTime + 100);
                    if (!isrunning)
                        Task.Factory.StartNew(scriptRegister);
                }
            }

            internal bool Changed = false;
            public const string REACT = "\n\t\t\t<script src='https://unpkg.com/react@latest/dist/react.min.js'></script>";
            public const string REACTDOM = "\n\t\t\t<script src='https://unpkg.com/react-dom@latest/dist/react-dom.min.js'></script>";
            public const string REACTDOMSERVER = "\n\t\t\t<script src='https://unpkg.com/react-dom@latest/dist/react-dom-server.min.js'></script>";
            public const string BABEL = "\n\t\t\t<script src='https://unpkg.com/babel-standalone@latest/babel.min.js'></script>";
            public const string JQUERY = "\n\t\t\t<script src='http://ajax.googleapis.com/ajax/libs/jquery/1/jquery.min.js'></script>";
            public const string BabelType = "\n\t\t<script type=\"text/babel\">\n\n";
            public const string RegularType = "\n\t\t<script>";
            public const string OPENINGHEADER = "<!DOCTYPE html>\n<html>\n\t<head>\n\t\t\n";
            public const string CLOSINGHEADER = "\n\t\t\t\n\t\t</head>\n\t\t<body>\n";
            public const string FOOTER = "\t</body>\n</html>";
            internal const string MOCKJQUERY = "https://raw.githubusercontent.com/MichaelFroelich/jquery-mockjax/master/src/jquery.mockjax.js";
            public bool InternalReact;
            public string Path;
            public bool IsSPA = true;
            public string Name;
            public object Props;
            public string Title = string.Empty;
            public string Style = string.Empty;
            public string Metadata = string.Empty;

            public List<string> Scripts = new List<string> {
                REACT,
                REACTDOM,/*
                BABEL,
                JQUERY*///Do not use Babel or jQuery, because Babel is massive and jQuery requires a mockscript
				string.Empty,
                string.Empty
            };

            public Dictionary<string, Script> ComponentScriptPathinfo = new Dictionary<string, Script>();

            public Func<string, string, object> Get { get; set; }
        }

        internal class Script
        {
            public bool isJSX;
            public string ScriptPath;
            public string ComponentScript;
            public string RenderedComponentScript;
            public long Size;
        }

        /// <summary>
        /// Folder to search first for Javascript files, this whole thing doesn't really work without react and maybe babel
        /// Default is Directory.GetCurrentDirectory() which is usually the directory where the binary is, probable elsewhere from Android
        /// The folder closest to the root but isolated from your webserver is best
        /// </summary>
        public static string JsFolder
        {
            get {
                return FEngine.JsFolder;
            }
            set {
                FEngine.JsFolder = value;
            }
        }

        private static bool isscript(string n) =>
        (n.EndsWith(".js") || istransformable(n) || n.EndsWith(".jsm") || n.EndsWith(".es") || char.IsDigit(n[n.Length - 1]));

        internal static bool istransformable(string n)
        => n.EndsWith(".jsx") || n.EndsWith(".ts") || n.EndsWith(".tsx");

        /// <summary>
        /// Minimum file size in bytes for loaded files, prevents badly named and badly placed fake files from causing exceptions when loaded
        /// </summary>
        public static int MinimumFileSize {get; set;} = 4;

        /// <summary>
        /// Function used to find the path of scripts. The source is fairly ugly but will find misplaced and even misnamed scripts
        /// </summary>
        /// <param name="scriptName">The name of the scripts</param>
        /// <param name="searchDepth">How many folders deep to search if the script isnt found</param>
        /// <returns></returns>
        public static string Search(string scriptname, int searchDepth = DEFAULTSEARCHDEPTH, Func<string,bool> FileTypeFilter = null)
        {
            if (File.Exists(scriptname) && (new FileInfo(scriptname).Length > MinimumFileSize)) {
                if (string.IsNullOrEmpty(System.IO.Path.GetDirectoryName(scriptname)))
                    scriptname = System.IO.Path.Combine(Directory.GetCurrentDirectory(), scriptname);
                return scriptname;
            }
            if (FileTypeFilter == null) FileTypeFilter = isscript;
            const string MIN = ".min";
            string ext = string.Empty;
            string potentialFolder = System.IO.Path.GetDirectoryName(scriptname);
            if (!string.IsNullOrEmpty(potentialFolder))
                scriptname = scriptname.Substring(potentialFolder.Length, scriptname.Length - potentialFolder.Length); //If a folder is found but a file isn't, assume misplaced file
            if (FileTypeFilter(scriptname))
                ext = scriptname.Substring(scriptname.LastIndexOf('.'), scriptname.Length - scriptname.LastIndexOf('.'));
            scriptname = scriptname.Remove(scriptname.Length - ext.Length, ext.Length);
            List<string> potentialFiles = null;
            potentialFolder = string.IsNullOrEmpty(potentialFolder) ? JsFolder : potentialFolder; //one last check
            for (int i = 0; i < searchDepth && (potentialFiles == null || potentialFiles.Count == 0); i++) {
                if (Directory.Exists(potentialFolder)) {
                    string toSearch = ext == string.Empty ? scriptname + "*" : scriptname + "*" + ext; //scripts always begin with their name, then have version information
                    potentialFiles = Directory.GetFiles(potentialFolder, "*" + toSearch, SearchOption.AllDirectories)
                        .Select(System.IO.Path.GetFullPath)
                        .Where(FileTypeFilter)
                        .Where(n => new FileInfo(n).Length > MinimumFileSize)
                        .ToList();
                }
                potentialFolder = System.IO.Path.Combine(potentialFolder, ".." + System.IO.Path.DirectorySeparatorChar);
            }

            if (potentialFiles.Count == 0) {
                return null;
            }
            string bestfile = null;
            int searchLength = (scriptname + ext).Length;
            int runningbest = 1000;
            int bestindex = 0;
            bool didyouthinkofmin = scriptname.Contains(MIN);
            string draw = string.Empty;
            for (int i = 0; i < potentialFiles.Count; i++) {
                int minallowance = (potentialFiles[i].Contains(MIN) && !didyouthinkofmin) ? MIN.Length : 0;
                var n = System.IO.Path.GetFileName(potentialFiles[i]);
                int currentAttempt = Math.Abs((n.Length - minallowance) - searchLength);
                if (currentAttempt == runningbest)
                    draw = potentialFiles[i];
                if (currentAttempt < runningbest) {
                    runningbest = currentAttempt;
                    bestindex = i;
                }
            }
            if (draw.Contains(MIN) && !potentialFiles[bestindex].Contains(MIN)) {
                bestfile = draw;
            }
            else
                bestfile = potentialFiles[bestindex];
            bestfile = string.IsNullOrEmpty(bestfile) ? potentialFiles[0] : bestfile;
            return bestfile;
        }
    }
}
