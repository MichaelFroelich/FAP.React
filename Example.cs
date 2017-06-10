using Newtonsoft.Json;
using System;
using System.Threading;

namespace FAP
{
    internal class Example
    {
        public static void Main(string[] args)
        {
            ReactivePage.DownloadScripts(); //This line is necessary if you have not downloaded scripts
            Index page = new Index();
            Server server = new Server(new[] { page });
            for(;;) Thread.Sleep(-1);
        }
    }

    /// <summary>
    /// This is the current basic cookie cutter of a FAP.ReactivePage
    /// Name this class the same as the Component name found in whatever script
    /// Using "nameof" allows you to rename everything C# side fairly quickly, right click and rename
    /// </summary>
    public class Index : ReactivePage
    {
        public Index() : base(nameof(Index), null)  //This constructor will actually be called by FAP.Server
        {                                               //Which isn't great, but all these functions are idempotent
            Engine.MinimiseBabelOutput = false;
            IncludeScript("main.jsx");      //Since this library is meant for cross platform and so more than one IDE
            IncludeCSS("css/main.css");     //Input files cannot be defined within the project or project files
            IncludeCSS("css/xbbcode.css");  //It must be defined in the code
            //IsSPA = true;                 //Due to the maturity of the IsSPA functionality, it's now set true as default
            get = GetDatabase;              //For proficient, safe development, this should be set with a state machine
          //(this as Page).get = (a,b) => JsonConvert.SerializeObject(GetDatabase(a,b)); //This also works, but why both?
        }
        
        public object GetDatabase(string a, string b)
        {
            return (new { name = "not the Computer, but in fact " + a });
        }
    }


}