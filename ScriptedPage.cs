using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FAP
{
    /// <summary>
    /// A significantly and deliberately lighter page than ReactivePage for the purpose of using JavaScript as an escape language
    /// In loaded HTML files, use {{ /* JavaScript HERE! */ }} this will even work with props such as, {{props.data}}
    /// Which will first attempt to caste the data as a string and then it will serialise the data. Also included is Handlebars.js.
    /// This class is binary serializable
    /// </summary>
    [Serializable]
    public class ScriptedPage : Page
    {
        /// <summary>
        /// The delimiters used to determine where the engine should begin running Javascript for vanilla javascript templating. 
        /// Whilst not recommended, this should be used if user entered data appearrs in the template files. Default is: "{{","}}"
        /// </summary>
        public static string[] Delimiters { get; set; } = new[] { "{{", "}}" };




        private Func<string, string, object> _get;

        /// <summary>
        /// Set a function which will be called when accessing this page through a "get" HTTP method. Return using
        /// Encoding.BigEndianUnicode for binary files (no warranties, no guarantees).
        /// </summary>
        /// <value>The get function</value>
        public Func<string, string, object> get
        {
            get {
                if (_get == null && StaticParent != null) _get = (StaticParent as ScriptedPage).get;
                return _get;
            }
            set {
                _get = value;
            }
        }

        /// <summary>
        /// Name used for the input data for vanilla templating, for other libraries you ay change this to whatever is innocuous.
        /// </summary>
        public static string PropsName { get; set; } = "props";
        /// <summary>
        /// Variable name used for passing in template strings. You may change this to whatever is innocuous.
        /// </summary>
        public static string SourceName { get; set; } = "__source";

        string newprops = null;
        string oldprops = "null";

        string oldtext;

        private string title;
        private string style;
        private List<string> scripts = new List<string>();
        private string name;
        private string metadata;

        object defaultProps = "null";
        /// <summary>
        /// Default props, defaults to a string of null so the internal engines can show their specific errors
        /// </summary>
        /// <value>The default properties.</value>
        public object DefaultProps
        {
            get {
                if (defaultProps == null && StaticParent != null)
                    defaultProps = (StaticParent as ScriptedPage).defaultProps;
                return defaultProps;
            }
            set {
                defaultProps = value;
            }
        }

        /// <summary>
        /// The title that displays on the browser window
        /// </summary>
        public string Title
        {
            get {
                if (title == null && StaticParent != null) {
                    title = (StaticParent as ScriptedPage).title;
                }
                else if (title == null)
                    title = "<title>" + Name + "</title>";
                return title.Substring(7, (title.Length - 8) - 7);
            }
            set {
                title = "<title>" + value + "</title>";
            }
        }

        /// <summary>
        /// The how your HTML is rendered from templates, vanilla means just props as data input and Javascript code execution in delimitted output. This replicates most features like {{props.name}}.
        /// </summary>
        public enum Templating
        {
            /// <summary>
            /// Only two features, runs code within delimitted regions and sets props as the input object. The props name can be changed and props can be changed within the code.
            /// </summary>
            Vanilla,
            /// <summary>Please ensure 
            ///Please ensure  handlebars.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            Handlebars,
            /// <summary>
            ///Please ensure  hogan.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            Hogan,
            /// <summary>
            ///Please ensure  moustache.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            Moustache,
            /// <summary>
            ///Please ensure  underscore.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            Underscore,
            /// <summary>
            ///Please ensure  dot.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            doT,
            /// <summary>
            ///Please ensure  ejs.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            EJS,
            /// <summary>
            /// Please ensure dust.js exists somewhere within the JsFolder or in a subfolder of the binary path
            /// </summary>
            Dust,
            /// <summary>
            /// Custom requires that CustomTemplate is defined or will throw an error
            /// </summary>
            Custom,

        }
        static bool[] hashad = new bool[(int)Templating.Custom + 1]; //this will never fail provided custom is always last

        Templating templateFramework = Templating.Vanilla;
        /// <summary>
        /// Includes handlebars.js as a library for server side rendering, this will search for the handlebars.js file around the JsFolder specified from FEngine
        /// </summary>
        public Templating TemplateFramework
        {
            get {
                if (StaticParent != null)
                    return (StaticParent as ScriptedPage).templateFramework;
                return templateFramework;
            }
            set {
                if (value != Templating.Vanilla && value != Templating.Custom && !hashad[(int)value]) {
                    hashad[(int)value] = true;
                    IncludeServerScript(value.ToString().ToLower() + ".js");
                }
                if (StaticParent != null)
                    (StaticParent as ScriptedPage).templateFramework = value;
                else
                    templateFramework = value;
            }
        }

        /// <summary>
        /// Adds styles to pages built with vue that do not use head or html tags in their template files
        /// </summary>
        public string Style
        {
            get {
                if (style == null && StaticParent != null) style = (StaticParent as ScriptedPage).style;
                return style;
            }
            internal set {
                style = value;
            }
        }
        /// <summary>
        /// If given a valid path to a local file, will include the CSS as an internal style sheet
        /// Otherwise, any string given will be considered a script source
        /// </summary>
        /// <param name="Pathname">Path or URL to the CSS file (ie yourwebsite.com/css/main.css)</param>
        /// <returns></returns>
        public void IncludeCSS(string Pathname)
        {
            string newstyle = null;
            if (File.Exists(Pathname))
                newstyle += "\t\t\t<style>\n" + File.ReadAllText(Pathname) + "\t\t\t</style>\n";
            else
                newstyle += "\t\t\t<link rel=\"stylesheet\" href=\"" + Pathname + "\">\n";
            if (!Style.Contains(newstyle)) {
                Style += newstyle;
            }
        }

        internal List<string> Scripts
        {
            get {
                if (scripts == null && StaticParent != null) scripts = (StaticParent as ScriptedPage).scripts;
                return scripts;
            }
            set {
                scripts = value;
            }
        }
        /// <summary>
        /// At default, is the filename without an extension of the file given and will contribute to the default value of the page Title
        /// </summary>
        public string Name
        {
            get {
                if (name == null && StaticParent != null) name = (StaticParent as ScriptedPage).name;
                return name;
            }
            set {
                name = value;
            }
        }
        /// <summary>
        /// Adds metadata to pages built with vue that do not use head or html tags in their template files
        /// </summary>
        public string Metadata
        {
            get {
                if (metadata == null && StaticParent != null) metadata = (StaticParent as ScriptedPage).metadata;
                return metadata;
            }
            internal set {
                metadata = value;
            }
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
            if (!Metadata.Contains(metad))
                Metadata += metad;
        }

        /// <summary>
        /// Please do not call this
        /// </summary>
        public ScriptedPage()
        {

        }

        /// <summary>
        /// Assumes the route path is the filepath without an extension 
        /// </summary>
        /// <param name="Path">path to the file</param>
        /// <param name="DefaultProps"></param>
        public ScriptedPage(string Path, object DefaultProps = null)
            : this(System.IO.Path.GetFileNameWithoutExtension(Path).ToLower(), Path, DefaultProps)
        {
        }

        /// <summary>
        /// This may be called at startup for each component that may be served via FAP
        /// Unlike ReactivePage, this has no need for component names and the props must be a string 
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="LocalPath">Local path or name of the file intended to be found</param>
        /// <param name="DefaultProps"></param>
        public ScriptedPage(string Path, string LocalPath, object DefaultProps = null, Templating Framework = Templating.Vanilla) : base(Path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(LocalPath);
            Name = name;
            Title = char.ToUpper(name[0]) + name.Substring(1);
            ReactivePage.Initialize();
            if (Rendercache == null)
                Rendercache = new Dictionary<int, Tuple<string, object>>();
            TemplateSegments = IncludeTemplate(LocalPath);

            if (DefaultProps != null) {
                if (DefaultProps is string) {
                    string propsstring = DefaultProps as string;
                    if ((propsstring[0] == '{' && propsstring[propsstring.Length - 1] == '}') ||
                        (propsstring[0] == '[' && propsstring[propsstring.Length - 1] == ']') ||
                        (propsstring[0] == '"' && propsstring[propsstring.Length - 1] == '"')) {
                        this.DefaultProps = JsonConvert.DeserializeObject<object>(propsstring);
                    }
                    newprops = propsstring;
                }
                else
                    this.DefaultProps = DefaultProps;
            }
            TemplateFramework = Framework;
            if (Framework != Templating.Vanilla && Framework != Templating.Custom) {
                ReactivePage.Engine.IncludeScript(new[] {
                    Framework.ToString().ToLower() + ".js"
                }, false, FEngine.Machine.Blank);
            }
            hashad[(int)Framework] = true;
            Task.Factory.StartNew(HotLoad);
        }
        ~ScriptedPage() { running = false; }
        bool running = true;
        async void HotLoad()
        {
            if (StaticParent != null)
                return;
            while (running) {
                await Task.Delay(ScriptReloadTime);
                if (TemplateSegments.FileSize != new FileInfo(TemplateSegments.Path).Length)
                    templateSegments = IncludeTemplate(TemplateSegments.Path);
            }
        }

        /// <summary>
        /// Custom template method, receives two strings, the template first and then the props
        /// </summary>
        public static Func<string, string, string> CustomTemplate { get; set; }

        /// <summary>
        /// Requires that a JSON string is returned by the get (lowercase) function, there is no intermediary scope here
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="messageContent"></param>
        /// <returns></returns>
        public override string Get(string queryString, string messageContent)
        {
            Tuple<string, object> tup = null;
            object objectprops = null;
            if (TemplateSegments == null)
                throw new Exception("Serious error, did you use the IncludeTemplate function so the page has a path to your html file?");
            try {
                if ((this as Page).get != null)
                    newprops = (this as Page).get(queryString, messageContent);
                else if (get != null) {
                    objectprops = get(queryString, messageContent);
                    if (objectprops is string) {
                        string propsstring = objectprops as string;
                        if ((propsstring[0] == '{' && propsstring[propsstring.Length - 1] == '}') ||
                            (propsstring[0] == '[' && propsstring[propsstring.Length - 1] == ']') ||
                            (propsstring[0] == '"' && propsstring[propsstring.Length - 1] == '"')) {
                            objectprops = JsonConvert.DeserializeObject<object>(propsstring);
                        }
                        newprops = propsstring;
                    }
                    else
                        newprops = JsonConvert.SerializeObject(objectprops);
                }
                if (newprops == null) {
                    newprops = JsonConvert.SerializeObject(DefaultProps);
                    objectprops = DefaultProps;
                }
                if ((TemplateSegments.Fresh || newprops != oldprops) && (!Rendercache.TryGetValue(newprops.GetHashCode() + TemplateSegments.Hashcode + (int)TemplateFramework, out tup))) {
                    oldprops = newprops;
                    if (TemplateFramework == Templating.Vanilla) {
                        oldtext = TemplateSegments.ToString(newprops);
                    }
                    else if (TemplateFramework == Templating.Custom) {
                        if (CustomTemplate == null) throw new Exception("the CustomTemplate cannot be null");
                        string templating = CustomTemplate(TemplateSegments.Source, newprops);
                        var trimed = templating.Trim();
                        if (trimed[0] == '<' && trimed[trimed.Length - 1] == '>') {
                            oldtext = templating;
                        }
                        else {
                            using (var engine = FEngine.BlankPool.GetContext()) {
                                oldtext = Template.GetString(engine.Instance.Execute(templating));
                            }
                        }
                    }
                    else {
                        using (var engine = FEngine.BlankPool.GetContext()) {
                            engine.Instance.SetVariable(SourceName, TemplateSegments.Source);
                            if (objectprops != null)
                                engine.Instance.SetVariable(PropsName, objectprops);
                            else
                                engine.Instance.Execute(PropsName + " = " + newprops + ";");
                            switch (TemplateFramework) {
                                case Templating.Handlebars:
                                    try { //sometimes setting a variable doesn't work, otherwise doing it like this can add features to some frameworks
                                        oldtext = Template.GetString(engine.Instance.Execute("Handlebars.compile(" + SourceName + ")(" + PropsName + ");"));
                                    }
                                    catch {
                                        oldtext = Template.GetString(engine.Instance.Execute("Handlebars.compile(" + SourceName + ")(" + newprops + ");"));
                                    }
                                    break;
                                case Templating.Hogan:
                                    try {
                                        oldtext = Template.GetString(engine.Instance.Execute("Hogan.compile(" + SourceName + ").render(" + PropsName + ");"));
                                    }
                                    catch {
                                        oldtext = Template.GetString(engine.Instance.Execute("Hogan.compile(" + SourceName + ").render(" + newprops + ");"));
                                    }
                                    break;
                                case Templating.Moustache:
                                    try {
                                        oldtext = Template.GetString(engine.Instance.Execute("Mustache.render(" + SourceName + "," + PropsName + ");"));
                                    }
                                    catch {
                                        oldtext = Template.GetString(engine.Instance.Execute("Mustache.render(" + SourceName + "," + newprops + ");"));
                                    }
                                    break;
                                case Templating.Underscore://sometimes setting a variable never works
                                    oldtext = Template.GetString(engine.Instance.Execute("_.template(" + SourceName + ")(" + newprops + ");"));
                                    break;
                                case Templating.doT:
                                    try {
                                        oldtext = Template.GetString(engine.Instance.Execute("doT.template(" + SourceName + ")(" + PropsName + ");"));
                                    }
                                    catch {
                                        oldtext = Template.GetString(engine.Instance.Execute("doT.template(" + SourceName + ")(" + newprops + ");"));
                                    }
                                    break;
                                case Templating.EJS:
                                    oldtext = Template.GetString(engine.Instance.Execute("ejs.render(" + SourceName + "," + newprops + ");"));
                                    break;
                                case Templating.Dust:
                                    oldtext = Template.GetString(engine.Instance.Execute("var __tout; dust.renderSource(" + SourceName + "," + newprops + ",function(err, out) { __tout = out; });__tout;"));
                                    break;
                                default:
                                    oldtext = TemplateSegments.ToString(objectprops ?? newprops);
                                    break;

                            }
                        }
                    }
                    if (!oldtext.StartsWith("<!DOCTYPE html>") && !oldtext.StartsWith("<html>")) { //A way to build a "SPA" simply by detecting if you need a SPA
                        oldtext = ReactivePage.SpaBuilder(false, oldtext, null, new ReactivePage.Component(false) {
                            Title = Title != null ? title : char.ToUpper(Name[0]) + Name.Substring(1),
                            Scripts = Scripts,
                            Metadata = Metadata,
                            Style = Style
                        });
                    }
                    TemplateSegments.Fresh = false;
                    Rendercache.Add(newprops.GetHashCode() + TemplateSegments.Hashcode + (int)TemplateFramework, new Tuple<string, object>(oldtext, null));
                }
                else if (tup != null && !string.IsNullOrEmpty(tup.Item1))
                    oldtext = tup.Item1;
            }
            catch (Exception e) {
                Console.Error.WriteLine("50: Scripted page error" + e.Message);
            }
            return oldtext ?? "<div></div>";
        }
        static Dictionary<int, Tuple<string, object>> Rendercache;

        Template templateSegments;
        Template TemplateSegments
        {
            get {
                if ((StaticParent as ScriptedPage) != null)
                    templateSegments = (StaticParent as ScriptedPage).TemplateSegments;
                return templateSegments;
            }
            set {
                templateSegments = value;
            }
        }
        /// <summary>
        /// Enable this to relax what may be printed from code escaped segments, in which without it only segments beginning with the propsname or not ending
        /// in a semicolon will print. Enabled, the last used variable will be printed.
        /// </summary>
        public static bool PrintLastUsedVariable { get; set; } = false;

        /// <summary>
        /// Includes a script that runs serverside to aid the script running within code segments of templates.
        /// This code is not hotloaded, unlike the template. It will also not appear client side.
        /// </summary>
        /// <param name="Pathname"></param>
        public void IncludeServerScript(string Pathname) => ReactivePage.Engine.IncludeScript(Pathname, ReactivePage.istransformable(Pathname), FEngine.Machine.Blank);
        /// <summary>
        /// Includes a script that runs serverside to aid the script running within code segments of templates.
        /// This code is not hotloaded, unlike the template. It will also not appear client side.
        /// </summary>
        /// <param name="Pathname"></param>
        public void IncludeServerScript(string Pathname, bool UseRenderMachine) => ReactivePage.Engine.IncludeScript(Pathname, UseRenderMachine, FEngine.Machine.Blank);
        /// <summary>
        /// Includes a script that runs serverside to aid the script running within code segments of templates.
        /// This code is not hotloaded, unlike the template. It will also not appear client side.
        /// </summary>
        /// <param name="Pathname"></param>
        public void IncludeServerScript(IEnumerable<string> Pathnames, bool UseRenderMachine) => ReactivePage.Engine.IncludeScript(Pathnames, UseRenderMachine, FEngine.Machine.Blank);

        /// <summary>
        /// For scripts such as libraries that are referred to from the server side JavaScript, use script tags yourself on the input HTML
        /// </summary>
        /// <param name="Path">Path to the script</param>
        public static void IncludeScript(string Path) => ReactivePage.Engine.IncludeScript(Path, ReactivePage.istransformable(Path), FEngine.Machine.Blank);

        /// <summary>
        /// If given a valid URL includes a script as a hosted source, otherwise it will load the script from file and even transform it from babel.
        /// All scripts given here will run client side, only script within the template or whatever you can link within there will run.
        /// These scripts are not hotloaded and are intended for client side libraries.
        /// </summary>
        /// <param name="Pathname">The pathname, beginning from the binary, to the JS/JSX script file to be included.</param>
        /// <param name="Type">Used if useRenderMachine is false, changes the type of <script type="text/TypeHere</param>
        public void IncludeScript(string Pathname, string Type = null)
        {
            string toadd = null;
            bool isJSX = ReactivePage.istransformable(Pathname);
            if (ReactivePage.IsURL(Pathname)) {
                if (isJSX) {
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
                if (isJSX)
                    actualscript = ReactivePage.Engine.TransformCode(actualscript);
                toadd = (string.Format("\n\t\t<script {0}>\n{1}\n\t\t</script>", Type == null ? string.Empty : "type=\"text/" + Type + "\"", actualscript));
            }
            else {
                string realpath = ReactivePage.Search(Pathname);
                if (realpath == null)
                    return;
                string actualscript = File.ReadAllText(realpath);
                if (ReactivePage.istransformable(realpath))
                    actualscript = ReactivePage.Engine.TransformCode(actualscript);
                toadd = (string.Format("\n\t\t<script {0}>\n{1}\n\t\t</script>", Type == null ? string.Empty : "type=\"text/" + Type + "\"", actualscript));
            }
            Scripts.Add(toadd);
        }
        /// <summary>
        /// Include a path to the template file and can be any text file as it has no filtering on its file extension besides if it ends in x, in which
        /// Recommended extensions are .html/.htm and .htmlx/.htmx for babel encoded javascript, transformation will applied to any script within the delimiters.
        /// </summary>
        /// <param name="Path"></param>
        public Template IncludeTemplate(string Path)
        {
            int offset = 0;
            int d1;
            int d2;
            string Delimiter1;
            string Delimiter2;
            string[] segments;
            if (!File.Exists(Path)) {
                Path = ReactivePage.Search(Path, 4, (a) => true);
            }
            bool requiresBabel = Path.EndsWith("x");
            var template = new Template(Path);
            string output = File.ReadAllText(Path);
            template.Source = output;
            template.FileSize = new FileInfo(Path).Length;
            Delimiter1 = Delimiters[0];
            Delimiter2 = Delimiters[1];
            while ((d1 = output.IndexOf(Delimiter1, offset)) >= 0 && (d2 = output.IndexOf(Delimiter2, offset)) > 0) {
                segments = new[] {
                    output.Substring(offset, d1 - offset),
                    output.Substring(d1 + Delimiter1.Length, d2 - d1 - Delimiter2.Length)
                };

                bool b = false;
                for (int i = 0; i < 2; i++) {
                    var segment = new Segment() {
                        RequiresRender = b,
                        RequiresBabel = requiresBabel,
                        Content = segments[i],
                        TransformedContent = (requiresBabel && b ? ReactivePage.Engine.TransformCode(segments[i]) : string.Empty)
                    };
                    b = !b;
                    template.Add(segment);
                }
                offset = d2 + Delimiters[1].Length; //offset ;)
                                                    //output = output.Substring(d2 + Delimiters[1].Length); //remove used section
            }
            if (output.Length > offset) {
                output = output.Substring(offset);
                bool endedwithservercode = output.StartsWith(Delimiters[0]);
                var segment = new Segment() {
                    RequiresRender = endedwithservercode, //if the last segment is code, dunno what you expect here
                    RequiresBabel = requiresBabel,
                    Content = endedwithservercode ? output.Substring(Delimiters[0].Length, (output.Length - Delimiters[0].Length)) : output,
                    TransformedContent = (requiresBabel && endedwithservercode ? ReactivePage.Engine.TransformCode(output) : string.Empty)
                };
                template.Add(segment);
            }
            return template;
        }
        /// <summary>
        /// Shortest interval in which new javascript has to be loaded, 5 seconds is good.
        /// </summary>
        /// <value>Time</value>
        public static int ScriptReloadTime { get; set; } = 500;


        /// <summary>
        /// A better class than Component
        /// </summary>
        [Serializable]
        public class Template
        {
            public string source;
            public string Source { get { return source; } set { source = value; } }
            public bool Fresh { get; set; } = true;
            public long FileSize { get; set; }
            public string Path { get; set; }
            public int Length { get; private set; } = 0;
            public int Hashcode { get; private set; } = 0;
            public List<Segment> Segments { get; set; } = new List<Segment>();

            public void Add(Segment segment)
            {
                segment.Index = Segments.Count;
                Length += string.IsNullOrEmpty(segment.TransformedContent) ? segment.Content.Length : segment.TransformedContent.Length;
                Hashcode += segment.Content.GetHashCode();
                Segments.Add(segment);
            }

            string lastresult;
            internal object lastprops;

            /// <summary>
            /// Returns true if the Hashcode property is correct 
            /// </summary>
            /// <returns></returns>
            public bool Checksum()
            {
                int runningsum = 0;
                foreach (Segment s in Segments)
                    runningsum += s.GetHashCode();
                return (runningsum == Hashcode);
            }

            public Template(string path)
            {
                Path = path;
            }

            /// <summary>
            /// Requires props as a JSON string, as in theory all JSON is legitimate JavaScript. This means serialisation is not performed internally.
            /// </summary>
            /// <param name="props"></param>
            /// <returns></returns>
            public string ToString(object props = null)
            {
                StringBuilder builder = new StringBuilder();
                string runningresult;
                if (props == null)
                    props = "null";
                if (lastprops == null)
                    lastprops = "null";
                object result = string.Empty;
                if (lastprops == null || !JToken.DeepEquals(JToken.FromObject(props), JToken.FromObject(lastprops)) || lastresult == null) {
                    lastprops = props;
                    object runningProps = props;
                    using (var blankengine = FEngine.BlankPool.GetContext()) {
                        if (props is string)
                            blankengine.Instance.Execute(PropsName + " = " + props + ";");
                        else
                            blankengine.Instance.SetVariable(PropsName, props);
                        var s = Segments.ToArray();
                        for (int i = 0; i < s.Length; i++) {
                            if (string.Empty == (s[i].Content))
                                continue;
                            var tup = new Tuple<string, object>(null, null);
                            if (!Rendercache.TryGetValue(runningProps.GetHashCode() + s[i].Content.GetHashCode(), out tup)) {
                                var requestedProps = runningProps;
                                if (s[i].RequiresRender) {
                                    if (s[i].RequiresBabel) {
                                        if (string.IsNullOrEmpty(s[i].TransformedContent)) s[i].TransformedContent = ReactivePage.Engine.TransformCode(s[i].Content);
                                        result = blankengine.Instance.Execute(s[i].TransformedContent);
                                    }
                                    else
                                        result = blankengine.Instance.Execute(s[i].Content);
                                    if (!PrintLastUsedVariable && !s[i].Content.Trim().StartsWith(PropsName) && s[i].Content.Trim().EndsWith(";"))
                                        result = string.Empty;
                                    runningProps = blankengine.Instance.GetVariable(PropsName);
                                }//Updates the props in case you got the brilliant idea to start playing with the props instead of just using it as data input
                                else {
                                    result = s[i].Content;
                                }//it's not necessary here
                                runningresult = GetString(result);
                                Rendercache.Add(requestedProps.GetHashCode() + s[i].Content.GetHashCode(), new Tuple<string, object>(runningresult, runningProps));
                            }
                            else {
                                runningresult = tup.Item1;
                                runningProps = tup.Item2;
                            }
                            builder.Append(runningresult);
                        }
                        lastresult = builder.ToString();
                    }
                }
                return lastresult;
            }

            /// <summary>
            /// Either gets a string or returns a serialisation depending on if is string evaluates to true
            /// Returns a new string of "null" if the object is null
            /// </summary>
            /// <param name="Object"></param>
            /// <returns>A definite string</returns>
            public static string GetString(object Object) => (Object is string) ? Object as string : Object != null ? JsonConvert.SerializeObject(Object) : String.Empty;

            public override string ToString()
            {
                return ToString(lastprops ?? "null");
            }

            public override int GetHashCode()
            {
                return Hashcode;
            }

        }
        [Serializable]
        public class Segment
        {
            public int Length { get; internal set; }
            internal bool HasChanged { get; set; }
            public int Index { get; internal set; }
            public bool RequiresRender { get; internal set; }
            public bool RequiresBabel { get; internal set; }
            public string Content { get; internal set; }
            public string TransformedContent { get; internal set; }
            public object CachedContent { get; internal set; }
        }
    }
}
