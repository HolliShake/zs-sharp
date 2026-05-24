// See https://aka.ms/new-console-template for more information

using zscript;

Func<string, string> ReadFile = File.ReadAllText;

Console.WriteLine("Hello, World!");

var path = "/home/andydevs69420/Documents/zscript/lang.txt";
var source = ReadFile(path);

var state = new State();

var compiler = new Compiler(state, path, source);
var script = compiler.Compile();

var vm = new Vm(state);
vm.MainLoop(script);