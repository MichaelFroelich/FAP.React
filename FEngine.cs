/*
                GNU GENERAL PUBLIC LICENSE
                           Version 3, 29 June 2007
     Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
     Everyone is permitted to copy and distribute verbatim copies
     of this license document, but changing it is not allowed.
        Author: Michael J. Froelich
 */
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VroomJs;
namespace FAP
{
    /// <summary>
    /// Engine class for rendering JSX files to HTML and for transforming JSX files to a more standardised script
    /// Please try not to instanstiate this class too much. Debug code within here for those working closely with the Js Engine
    /// </summary>
    public class FEngine
    {
        /// <summary>
        /// Will either load a script from this path if a path is stored
        /// or it will search around the binary for a script with this in its filename
        /// Defaults are in this order: Babel, requirejs (but will not flag an error if missing)
        /// </summary>
        public List<string> BabelScriptPaths { get; set; } = new List<string>(new string[] { "babel" });

        /// <summary>
        /// Will either load a script from this path if a path is stored or it will search around the binary for a script with this in its filename
        /// These scripts will not update on runtime but will update all instances when accessed
        /// Defaults are in this order: React, ReactDOMServer, requirejs (but will not flag an error if missing)
        /// </summary>
        public List<string> ReactScriptPaths { get; set; } = new List<string>(new string[] { "react", "react-dom-server" });
        /// <summary>
        /// JsPool of instances used to transform script, consume by using(var engine = BabelPool.GetContext()) {
        /// Contains Babel
        /// Common functions are engine.Execute and engine.Set/engine.Get variables
        /// </summary>
        public static JsPool BabelPool;
        /// <summary>
        /// JsPool of instances used to render HTML, consume by using(var engine = ReactPool.GetContext()) {
        /// Contains React, React-DOM-Server and anything included as a "mock" script
        /// Common functions are engine.Execute and engine.Set/engine.Get variables
        /// </summary>
        public static JsPool ReactPool;

#if DEBUG
        public List<string> BlankScriptPaths { get; set; } = new List<string>();
        public static JsPool BlankPool;

        private string blankscript;
        private string blankScriptGet() => blankscript ?? String.Empty;
#endif
        private string babelscript;
        private string babelScriptGet() => babelscript ?? String.Empty; //Used to dynamically get the complete scripts

        private string reactscript;
        private string reactScriptGet() => reactscript ?? String.Empty;

        /// <summary>
        /// Whatever you give me, know that the purpose of this entire class is to run babel.js
        /// A word of caution to the lazy, someone who expects you to forget will begin to expect
        /// Consequences.
        /// </summary>
        /// <param name="babelScriptPaths">Scripts run between loading Babel and performing a Transform function from Babel</param>
        /// <param name="reactScriptPaths">Scripts run before performing a Render function from React, finished component scripts etc</param>
        /// <param name="blankScriptPaths">Unused to keep memory down, but would have enabled a free sandbox for scripts</param>
        public FEngine(List<string> babelScriptPaths = null, List<string> reactScriptPaths = null, List<string> blankScriptPaths = null)
        {
            string requireisnice = null;
            RenderCache = new Dictionary<int, string>();
            if (!AssemblyLoader._isLoaded && IsWindows) {
                AssemblyLoader.EnsureLoaded(); //The three lines of code too difficult for the officialised system
            }
            if (reactScriptPaths != null) {
                ReactScriptPaths = reactScriptPaths;
                if (ReactivePage.JsFolder == Directory.GetCurrentDirectory().ToString() && File.Exists(reactScriptPaths.FirstOrDefault())) {
                    ReactivePage.JsFolder = Path.GetDirectoryName(reactScriptPaths[0]); //Get this away from what's usually the executing binary as soon as possible
                }
            }
            else {
                for (int i = 0; i < ReactScriptPaths.Count; i++) {
                    string p;
                    if ((p = ReactivePage.Search(ReactScriptPaths[i])) == null) {
                        throw new Exception("Cannot find essential script " + ReactScriptPaths[i] + "\nEither set the JS folder from ReactivePage.JsFolder or run the ReactivePage.DownloadScripts function.");
                    }
                    else
                        ReactScriptPaths[i] = p;
                }
            }
            if ((requireisnice = ReactivePage.Search("require")) != null && !ReactScriptPaths.Contains("require"))
                ReactScriptPaths.Add(requireisnice); //by default, it's nice to allow require and import like lines, if you're into that and have it around

            if (babelScriptPaths != null)
                BabelScriptPaths = babelScriptPaths;
            else {
                for (int i = 0; i < BabelScriptPaths.Count; i++) {
                    if ((BabelScriptPaths[i] = ReactivePage.Search(BabelScriptPaths[i])) == null) {
                        Console.Error.WriteLine("Cannot find babel.js this means any script requiring transformation included will throw errors");
                    }
                }
            }
            if (!string.IsNullOrEmpty(requireisnice) && !ReactScriptPaths.Contains("require")) {
                BabelScriptPaths.Add(requireisnice);
            }
#if DEBUG
            if (blankScriptPaths != null)
                BlankScriptPaths = blankScriptPaths;
            else
                for (int i = 0; i < BlankScriptPaths.Count; i++)
                    BlankScriptPaths[i] = ReactivePage.Search(BlankScriptPaths[i]);
            IncludeScripts(BlankScriptPaths, false, Machine.Blank);
            if (BlankPool == null)
                BlankPool = new JsPool(babelScriptGet);
#endif
            IncludeScripts(ReactScriptPaths, false, Machine.React);
            IncludeScripts(BabelScriptPaths, false, Machine.Babel);
        }

        /// <summary>
        /// Enum used for specifying which machine to add scripts to, adding scripts to the React machine
        /// enables validation for scripts passed into that machine whereas adding scripts to the Babel
        /// machine enables validation for scripts being transformed. It all depends where you're getting
        /// invalidation errors, at program startup or upon connecting with a browser.
        /// </summary>
        public enum Machine
        {
            /// <summary>
            /// The react render engine, these scripts are run when a user connects
            /// </summary>
            React,
            /// <summary>
            /// The babel transformation engine, these scripts are run when loaded so only plain JS is used
            /// </summary>
            Babel,
            //Blank
        }

        /// <summary>
        /// Includes or executes these scripts on the startup of either Js Context: the render method or babel transform
        /// </summary>
        /// <param name="Pathname">Pathname/Path to the script to include</param>
        /// <param name="UseRenderMachine">Whether or not to transform the code before passing it into the context</param>
        /// <param name="machine">Either FAP.Machine.Render or FAP.Machine.Babel or FAP.Machine.Blank</param>
        /// <returns></returns>
        public bool IncludeScript(string Pathname, bool UseRenderMachine = false, Machine machine = Machine.React)
        {
            return IncludeScripts(new[] { Pathname }, UseRenderMachine, machine);
        }
        /// <summary></summary>
        /// <param name="Pathname">Pathname/Path to the script to include</param>
        /// <param name="UseRenderMachine">Whether or not to transform the code before passing it into the context</param>
        /// <param name="machine">Either FAP.Machine.Render or FAP.Machine.Babel or FAP.Machine.Blank</param>
        /// <returns></returns>
        public bool IncludeScripts(IEnumerable<string> Pathname, bool UseRenderMachine = false, Machine machine = Machine.React)
        {
            List<string> scriptsToWork = null;
            List<string> paths = new List<string>();
            var inputcopy = Pathname.ToArray(); //IEnumerables aren't nice to work with
            foreach (string s in inputcopy)
                paths.Add(ReactivePage.Search(s)); //ensures paths
            bool HasList = true;
            switch (machine) {
                case Machine.React:
                    scriptsToWork = ReactScriptPaths;
                    break;
                case Machine.Babel:
                    scriptsToWork = BabelScriptPaths;
                    break;
#if DEBUG
                case Machine.Blank:
                    scriptsToWork = BlankScriptPaths;
                    break;
#endif
                default:
                    throw new Exception("Not supported");
            }
            string scripttowork = string.Empty;
            foreach (string s in inputcopy) //removes any unensured paths
                if (scriptsToWork.Contains(s))
                    scriptsToWork.Remove(s);
            scriptsToWork.AddRange(paths);

            paths.ForEach(s => HasList &= File.Exists(s)); //One last very quick check..
            if (HasList) {
                scripttowork = concatScripts(paths, UseRenderMachine);
                switch (machine) {
                    case Machine.React:
                        reactscript += scripttowork;
                        ReactPool = new JsPool(reactScriptGet);
                        break;
#if DEBUG
                    case Machine.Blank:
                        blankscript += scripttowork;
                        BlankPool = new JsPool(blankScriptGet);
                        break;
#endif
                    case Machine.Babel:
                        babelscript += scripttowork;
                        BabelPool = new JsPool(babelScriptGet);
                        break;
                }
            }
            else
                throw new Exception("22: Engine include script error, non existent paths in the script path list of " + machine.ToString());
            return true;
        }

        private string concatScripts(List<string> scripts, bool please)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string scripttocompile in scripts) {
                if (scripttocompile.EndsWith(".jsx") || please)
                    sb.Append(TransformCode(File.ReadAllText(scripttocompile))).Append("\n;\n");
                else
                    sb.Append(File.ReadAllText(scripttocompile)).Append("\n;\n");
            }
            return sb.ToString();
        }
        /// <summary>
        /// ID used on the first div element and passed by to the ReactDOM function, default is rootComponent
        /// I have only a hazy idea why you'd want to change it
        /// </summary>
        public string RootComponentId { get; set; } = "rootComponent";

        private string GetReactRenderScript(string ComponentName, string props) =>
        string.Format("ReactDOMServer.renderToString(React.createElement({0},{1}));", ComponentName, props);
        private string GetHtmlRenderScript(string ComponentName, string props) =>
        string.Format("ReactDOMServer.renderToStaticMarkup(React.createElement({0},{1}));", ComponentName, props);
        /// <summary>
        /// Provides one line of script as a string used for attaching the ReactJS engine client side to the RootComponentId
        /// </summary>
        /// <returns>The java script</returns>
        /// <param name="ComponentName">Component name</param>
        /// <param name="props">Properties serialised into a string</param>
        public string RenderJavaScript(string ComponentName, object props) =>
        RenderJavaScript(ComponentName, JsonConvert.SerializeObject(props));
        /// <summary>
        /// Provides one line of script as a string used for attaching the ReactJS engine client side to the RootComponentId
        /// </summary>
        /// <returns>The java script</returns>
        /// <param name="ComponentName">Component name</param>
        /// <param name="props">Properties serialised into a string</param>
        public string RenderJavaScript(string ComponentName, string props) =>
        string.Format("ReactDOM.render(React.createElement({0},{1}), document.getElementById('{2}'));", ComponentName, props, RootComponentId);

        /// <summary>
        /// Renders the html.
        /// </summary>
        /// <returns>The html</returns>
        /// <param name="ComponentName">Component name</param>
        /// <param name="props">Properties serialised into a string</param>
        /// <param name="InputScripts">Input scripts</param>
        public string RenderHtml(string ComponentName, string props, IEnumerable<string> InputScripts = null) => RenderHtml(ComponentName, props, false, InputScripts);
        /// <summary>
        /// Renders the html.
        /// </summary>
        /// <returns>The html</returns>
        /// <param name="ComponentName">Component name</param>
        /// <param name="props">Properties as a serialisable object</param>
        /// <param name="InputScripts">Input scripts</param>
        public string RenderHtml(string ComponentName, object props, IEnumerable<string> InputScripts = null) => RenderHtml(ComponentName, props, false, InputScripts);

        /// <param name="HtmlOnly">If true, react information shall not be included</param>
        /// <returns></returns>
        public string RenderHtml(string ComponentName, object props, bool HtmlOnly = false, IEnumerable<string> InputScripts = null)
        {
            string sprops = JsonConvert.SerializeObject(props);
            return RenderHtml(ComponentName, sprops, HtmlOnly, InputScripts);
        }
        /// <summary>
        /// Renders the html.
        /// </summary>
        /// <returns>The html</returns>
        /// <param name="ComponentName">Component name</param>
        /// <param name="props">Properties</param>
        /// <param name="HtmlOnly">If set to <c>true</c> html only</param>
        /// <param name="InputScripts">Input scripts</param>
        public string RenderHtml(string ComponentName, string props, bool HtmlOnly = false, IEnumerable<string> InputScripts = null)
        {
            ReactivePage.Component dangerousidea;
            if (ReactivePage.defaults.TryGetValue(ComponentName.ToLower(), out dangerousidea)) {
                return RenderHtml(ComponentName, props, HtmlOnly, dangerousidea, InputScripts);
            }
            return RenderHtml(ComponentName, props, HtmlOnly, null, InputScripts);
        }
        internal string RenderHtml(string ComponentName, string props, bool HtmlOnly = false, ReactivePage.Component components = null, IEnumerable<string> InputScripts = null)
        {
            StringBuilder renderBuild = new StringBuilder();
            string scriptnameforvroom = "Anonymous";
            if (components != null && components.ComponentScriptPathinfo.Count > 0) {
                foreach (ReactivePage.Script s in components.ComponentScriptPathinfo.Values) {
                    if (string.IsNullOrEmpty(s.RenderedComponentScript))
                        renderBuild.AppendLine(s.ComponentScript);
                    else
                        renderBuild.AppendLine(s.RenderedComponentScript);

                }
                scriptnameforvroom = Path.GetFileName(components.ComponentScriptPathinfo.Last().Value.ScriptPath); //Assumedly, the last script would contain the renderable component
            }
            if (InputScripts != null) {
                foreach (string s in InputScripts)
                    renderBuild.AppendLine(s);
            }
            if (HtmlOnly)
                renderBuild.Append(GetHtmlRenderScript(ComponentName, props));
            else
                renderBuild.Append(GetReactRenderScript(ComponentName, props));
            string toRender = renderBuild.ToString();
            string toReturn;
            int hash = toRender.GetHashCode() + props.GetHashCode(); //It's still faster to hash both these and check if something with this script and props has come than to run javascript
            if (!RenderCache.TryGetValue(hash, out toReturn)) {
                using (var pool = ReactPool.GetContext()) {
                    var instance = pool.Instance;
                    var output = instance.Execute(toRender, scriptnameforvroom);
                    string html = output as string;
                    toReturn = string.Format("<div id='{0}'>{1}</div>", RootComponentId, html);
                }
                RenderCache.Add(hash, toReturn);
            }
            return toReturn;
        }
        Dictionary<int, string> RenderCache; //haxxy cache to prevent someone spamming from creating contexes

        /// <summary>
        /// Minimises the output gained from performing the Transform functions. Default is false.
        /// </summary>
        public bool MinimiseBabelOutput { get; set; } = true;
        public List<string> BabelPresets { get; set; } = new List<string> {
            "stage-2",//Saves 10ms against other stages when benchmarking debug, as of writing
			"es2015",
            "react"
        };

        /// <summary>
        /// Currently unsure how these are used, assumedly including these here and as "mock scripts" from reactive page enables middleware?
        /// </summary>
        public List<string> BabelPlugins { get; set; } = new List<string>();

        /// <summary>
        /// Parser options sent to Babylon, what's really transforming code. Set this by making it equal to an anonymous object, such as:
        /// ParserOptions = new { allowImportExportEverywhere = true, allowReturnOutsideFunction = true };
        /// Which are the defaults, since you'd rather more code than less transforming. Alert me if plugins or presets cease working, 
        /// it would be this line of code here. Set to null for the Babel/Babylon's true default.
        /// </summary>
        public object ParserOptions { get; set; } = new { allowImportExportEverywhere = true, allowReturnOutsideFunction = true };

        private const string RenderOutputVariable = "_FAP_Render_Output";
        private const string RenderInputVariable = "_FAP_Render_Input";
        private const string ParserOptionsConst = ", parserOpts: ";
        private readonly string TransformCodeScriptDebug = RenderOutputVariable + " = Babel.transform(" + RenderInputVariable + ", {  retainLines: true, presets: ";
        private readonly string TransformCodeFileDebug = RenderOutputVariable + " = Babel.transformFile(" + RenderInputVariable + ", { retainLines: true, presets: ";
        private readonly string TransformCodeScript = RenderOutputVariable + " = Babel.transform(" + RenderInputVariable + ", {minified: true, comments: false, presets: ";
        private readonly string TransformCodeFile = RenderOutputVariable + " = Babel.transformFile(" + RenderInputVariable + ", {minified: true, comments: false, presets: ";
        private readonly string TransformCodeTrailer = "}).code;";
        /// <summary>
        /// Calls the internal Babel transform function with Pathname passed in as the first parameter
        /// </summary>
        /// <param name="Pathname"></param>
        /// <returns>Null if failure</returns>
        public string TransformFile(string Pathname, string ScriptName = "Anonymous")
        {
            string toret = null;
            try {
                if (ScriptName == "Anonymous")
                    ScriptName = Path.GetFileName(Pathname);
                using (var instance = BabelPool.GetContext()) {
                    string plugins = string.Empty;
                    if (BabelPlugins.Count > 0)
                        plugins = ", plugins: " + JsonConvert.SerializeObject(BabelPlugins);
                    instance.Instance.SetVariable(RenderInputVariable, Pathname);
                    if (!MinimiseBabelOutput)
                        instance.Instance.Execute(
                            TransformCodeFileDebug + JsonConvert.SerializeObject(BabelPresets) + plugins +
                            (ParserOptions != null ? ParserOptionsConst + JsonConvert.SerializeObject(ParserOptions) : string.Empty) +
                            TransformCodeTrailer, ScriptName);
                    else
                        instance.Instance.Execute(
                            TransformCodeFile + JsonConvert.SerializeObject(BabelPresets) + plugins +
                            (ParserOptions != null ? ParserOptionsConst + JsonConvert.SerializeObject(ParserOptions) : string.Empty) +
                            TransformCodeTrailer, ScriptName);
                    toret = instance.Instance.GetVariable(RenderOutputVariable) as string;
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine("20: Babel Transformation Error\n" + e.Message);
            }
            return toret;
        }
        public string TransformCode(string code, string ScriptName = "Anonymous")
        {//scriptnameforvroom = Path.GetFileName(components.ComponentScriptPathinfo.FirstOrDefault().Value.ScriptPath);
            string toret = null;
            try {
                using (var instance = BabelPool.GetContext()) {
                    string plugins = string.Empty;
                    if (BabelPlugins.Count > 0)
                        plugins = ", plugins: " + JsonConvert.SerializeObject(BabelPlugins);
                    instance.Instance.SetVariable(RenderInputVariable, code);
                    if (!MinimiseBabelOutput)
                        instance.Instance.Execute(
                            TransformCodeScriptDebug + JsonConvert.SerializeObject(BabelPresets) + plugins +
                            (ParserOptions != null ? ParserOptionsConst + JsonConvert.SerializeObject(ParserOptions) : string.Empty) +
                            TransformCodeTrailer, ScriptName);
                    else
                        instance.Instance.Execute(
                            TransformCodeScript + JsonConvert.SerializeObject(BabelPresets) + plugins +
                            (ParserOptions != null ? ParserOptionsConst + JsonConvert.SerializeObject(ParserOptions) : string.Empty) +
                            TransformCodeTrailer, ScriptName);
                    toret = instance.Instance.GetVariable(RenderOutputVariable) as string;
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine("20: Babel Transformation Error\n" + e.Message);
            }
            return toret;
        }
        static bool IsWindows => (Environment.OSVersion.Platform.ToString().StartsWith("W"));
    }
    public class Poolable : IDisposable
    {
        public ConcurrentQueue<Poolable> parent;
        /// <summary>
        /// Lifetime of any poolable objects in seconds. 
        /// A minute supposes short bursts of use, an hour (3600) supposes long periods of heavy use, 5 seconds for the memory conscious only
        /// </summary>
        public static int PoolLife { get; set; } = 60;
        internal DateTime LastUsed;
        public Poolable(JsContext obj, ConcurrentQueue<Poolable> Parent)
        {
            this.Instance = obj;
            this.parent = Parent;
            LastUsed = DateTime.UtcNow;
            if (JsPool.UseMinimalMemory == true) {
                System.Threading.Tasks.Task.Factory.StartNew(Conserve);
            }
            //parent.Pool.Enqueue(this);
        }
        internal async void Conserve()
        {
            while (parent != null) {
                await System.Threading.Tasks.Task.Delay(PoolLife * 1000);
                if (DateTime.UtcNow.Subtract(LastUsed).Seconds > PoolLife && parent.Count > JsPool.MinSize) {
                    Poolable throwaway;
                    parent.TryDequeue(out throwaway);
                    if (throwaway.Instance is IDisposable)
                        (throwaway.Instance as IDisposable).Dispose();
                    throwaway = null;
                }
            }
        }
        public JsContext Instance { get; internal set; }
        public void Dispose()
        {
            if (Instance != null && parent.Count < JsPool.MaxSize)
                parent.Enqueue(this);
        }
    }
    public class JsPool
    {
        /// <summary>
        /// The maximum number allowable within the queue
        /// Default is 100, it's unlikely to ever get that far
        /// </summary>
        public static int MaxSize { get; set; } = 100; //As of writing, the entire system responds in 10ms which means a max 100 requests a second
                                                       /// <summary>
                                                       /// The number of JsContexts in the queue, one is default and good for one user at one time
                                                       /// Three is ideal for busier sites
                                                       /// </summary>
        public static int MinSize { get; set; } = 1;
        /// <summary>
        /// If over the minimum JsContext size and if set to true, JsContexts will begin deleting themselves after PoolLife seconds.
        /// </summary>
        public static bool UseMinimalMemory = true;
        /// <summary>
        /// Freely accessible JsPool actual pool, a queue, for mischief
        /// </summary>
        public ConcurrentQueue<Poolable> Pool;
        private Func<JsContext> Generator;
        /// <summary>
        /// Input is a function that returns a string, this allows the JsPool to dynamically use script strings on regeneration
        /// </summary>
        /// <param name="StringGenerator"></param>
        public JsPool(Func<string> ScriptGenerator)
        {
            Pool = new ConcurrentQueue<Poolable>();
            Generator = () => {
                string Script = ScriptGenerator();
                var dasengine = new JsEngine(-1, -1);
                var newcontext = dasengine.CreateContext();
                if (!string.IsNullOrEmpty(Script))
                    newcontext.Execute(Script);
                return newcontext;
            };
            Load();
        }

        private void Load()
        {
            for (int i = 0; i < MinSize; i++)
                Pool.Enqueue(new Poolable(Generator(), Pool));
        }
        /// <summary>
        /// Don't use this. It's an extremely bad habit.
        /// </summary>
        public JsContext GetInstance => GetContext().Instance;
        /// <summary>
        /// Literally use this such as using(var Pool = JsPool.GetObject())
        /// </summary>
        /// <returns></returns>
        public Poolable GetContext()
        {
            if (Pool != null && Pool.Count > 0) {
                Poolable output;
                if (Pool.TryDequeue(out output))
                    return output;
            }
            return new Poolable(Generator(), Pool);
        }
    }
}