#if ((!(DEBUG || Verbose)) || forceIndex) && allowIndex
#define useIndex
#endif

#define UseCil //need to change this in VMTestsBase.cs too!

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Prexonite;
using Prexonite.Commands;
using Prexonite.Compiler;
using Prexonite.Compiler.Ast;
using Prexonite.Compiler.Cil;
using Prexonite.Types;

using Prx.Tests;

// ReSharper disable CheckNamespace
namespace Prx.Tests
// ReSharper restore CheckNamespace
{
// ReSharper disable InconsistentNaming
    public abstract class VMTests : VMTestsBase
// ReSharper restore InconsistentNaming
    {

        #region Setup

        #endregion

        [Test]
        public virtual void CilVersusInterpreted()
        {
            Compile(@"
function main()
{
    var interpreter_stack = asm(ldr.eng).Stack.Count;
    println(interpreter_stack);
    return interpreter_stack == 0;
}
");
            Expect(CompileToCil);
        }

        [Test]
        public void Basic()
        {
            const string input1 = @"
function test1
{
    var x = 5 + 5;
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            Assert.AreEqual(0, ldr.ErrorCount);

            var test1 = target.Functions["test1"];
            var fctx = new FunctionContext(engine, test1);
            var x = fctx.LocalVariables["x"];
            Assert.IsTrue(
                x.Value == null || x.Value.Value == null, "variable x must be null in some way.");
            engine.Stack.AddLast(fctx);
            engine.Process();
            Assert.AreEqual(
                0, engine.Stack.Count, "Machine stack is expected to be empty after execution.");
            Assert.IsNotNull(x.Value, "Value of PVariable is null (violates invariant).");
            Assert.AreEqual(PType.BuiltIn.Int, x.Value.Type.ToBuiltIn());
            Assert.IsNotNull(x.Value.Value, "Result is null (while PType is Int)");
            Assert.AreEqual(10, (int) x.Value.Value);
        }

        [Test]
        public void IncDecrement()
        {
            const string input1 =
                @"
function test1(x)
{
    x++;    
    x = 2*x++;
    return x--;
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            foreach (var s in ldr.Errors)
                Console.WriteLine(s);

            Assert.AreEqual(0, ldr.ErrorCount, "Errors during compilation");

            var rnd = new Random();
            var x0 = rnd.Next(1, 200);
            var x = x0;
            x++;
            x = 2*x;
            var expected = x--;

            var fctx =
                target.Functions["test1"].CreateFunctionContext(engine, new PValue[] {x0});
            engine.Stack.AddLast(fctx);
            var rv = engine.Process();

            Assert.AreEqual(PType.BuiltIn.Int, rv.Type.ToBuiltIn());
            Assert.AreEqual(
                expected,
                (int) rv.Value,
                "Return value is expected to be " + expected + ".");

            Assert.AreEqual(
                x,
                (int) fctx.LocalVariables["x"].Value.Value,
                "Value of x is supposed to be " + x + ".");
        }

        [Test]
        public void LateReturnIsIllegal()
        {
            const string input1 =
                @"
function test1(x)
{
    x*=2;
    return = x-2;
    x+=55;
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            foreach (var s in ldr.Errors)
                Console.WriteLine(s);
            Assert.AreEqual(1, ldr.ErrorCount, "One error expected.");
            Assert.IsTrue(ldr.Errors[0].Message.Contains("Return value assignment is no longer supported."),"The compiler did not reject a return value assignment.");

        }

        [Test]
        public void Return()
        {
            const string input1 =
                @"
function twice(v) = 2*v;
function complicated(x,y) does
{
    var z = x*y;
    x = z-x;
    y = x+z;
    //z     := x*y
    //x     := x*y-x
    //y     := 2*x*y-x
    //y+z   := 3*x*y-2*x
    //y+z   := x*(3*y-2)
    return y+x;
    //dummy     
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            foreach (var s in ldr.Errors)
                Console.WriteLine(s);
            Assert.AreEqual(0, ldr.ErrorCount);

            var rnd = new Random();

            //Test simple
            var v0 = rnd.Next(1, 100);
            var expected = 2*v0;

            var result = target.Functions["twice"].Run(engine, new PValue[] {v0});
            Assert.AreEqual(
                PType.BuiltIn.Int,
                result.Type.ToBuiltIn(),
                "Result is expected to be an integer. (twice)");
            Assert.AreEqual(expected, (int) result.Value);

            //Test complicated            
            var x0 = rnd.Next(1, 100);
            var y0 = rnd.Next(1, 100);
            var z = x0*y0;
            var x1 = z - x0;
            var y1 = x1 + z;
            expected = y1 + x1;

            result = target.Functions["complicated"].Run(engine, new PValue[] {x0, y0});
            Assert.AreEqual(
                PType.BuiltIn.Int,
                result.Type.ToBuiltIn(),
                "Result is expected to be an integer. (complicated)");
            Assert.AreEqual(expected, (int) result.Value);
        }

        [Test]
        public void FunctionAndGlobals()
        {
            const string input1 =
                @"
var J; //= random();
function h(x) = x+2+J;
function test1(x) does
{
    J = 0;
    x = h(J);
    J = h(7*x);
    return h(x)/J;
}
";
            var rnd = new Random();
            var j = rnd.Next(1, 1000);

            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            target.Variables["J"].Value = j;
            Assert.AreEqual(0, ldr.ErrorCount);

            Console.WriteLine(target.StoreInString());

            //Expectation
            var x0 = rnd.Next(1, 589);
            j = 0;
            j = (7*x0 + 2 + j);
            var expected = (x0 + 2 + j)/j;

            var fctx =
                target.Functions["test1"].CreateFunctionContext(engine, new PValue[] {x0});
            engine.Stack.AddLast(fctx);
            var rv = engine.Process();
            Assert.AreEqual(PType.BuiltIn.Int, rv.Type.ToBuiltIn());
            Assert.AreEqual(expected, (int) rv.Value);
        }

        [Test]
        public void FunctionCallSimple()
        {
            const string input1 =
                @"
var J;
function h(x) = 2+x+J;
function test1(x) does
{
    J = 0;
    x = h(J);
    J = h(7*x);
    return h(x)/J;
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            Assert.AreEqual(0, ldr.ErrorCount);

            Console.WriteLine(target.StoreInString());

            var rnd = new Random();
            const int j0 = 0;
            var x0 = rnd.Next(1, 300);
            const int x1 = 2 + j0 + j0;
            const int j1 = 2 + (7*x1) + j0;
            const int expected = (2 + x1 + j1)/j1;

            var fctx =
                target.Functions["test1"].CreateFunctionContext(engine, new PValue[] {x0});
            engine.Stack.AddLast(fctx);
            var rv = engine.Process();
            Assert.AreEqual(PType.BuiltIn.Int, rv.Type.ToBuiltIn());
            Assert.AreEqual(expected, (int) fctx.ReturnValue.Value);

            Assert.AreEqual(PType.BuiltIn.Int, target.Variables["J"].Value.Type.ToBuiltIn());
            Assert.AreEqual(j1, (int) target.Variables["J"].Value.Value);
        }

        [Test]
        public void FibRecursion()
        {
            const string input1 =
                @"
function fib(n) does
{
    if(n <= 2)
        return 1;
    else
        return fib(n-1) + fib(n-2);
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            Assert.AreEqual(0, ldr.ErrorCount);

            for (var n = 1; n <= 6; n++)
            {
                Console.WriteLine("\nFib(" + n + ") do ");
                var expected = _fibonacci(n);
                var fctx =
                    target.Functions["fib"].CreateFunctionContext(engine, new PValue[] {n});
                engine.Stack.AddLast(fctx);
                var rv = engine.Process();
                Assert.AreEqual(
                    PType.BuiltIn.Int, rv.Type.ToBuiltIn(), "Result must be a ~Int");
                Assert.AreEqual(
                    expected,
                    (int) rv.Value,
                    "Fib(" + n + ") = " + expected + " and not " + (int) rv.Value);
            }
        }

        [Test]
        public void Recursion()
        {
            const string input1 =
                @"
function fib(n) does asm
{
    //if n <= 2
    ldloc   n
    ldc.int 2
    cle
    jump.f  else
    //return 1;
    ldc.int 1
    ret.value
    jump    endif
    //else do
    label   else
    //return = fib(n-1) + fib(n-2);
    ldloc   n
    ldc.int 1
    sub
    func.1  fib
    ldloc   n
    ldc.int 2
    sub
    func.1  fib
    add
    ret.value
    
    label   endif
}
";
            var ldr = new Loader(engine, target);
            ldr.LoadFromString(input1);
            Assert.AreEqual(0, ldr.ErrorCount);

            for (var n = 1; n <= 6; n++)
            {
                Console.WriteLine("\nFib(" + n + ") do ");
                var expected = _fibonacci(n);
                var fctx =
                    target.Functions["fib"].CreateFunctionContext(engine, new PValue[] {n});
                engine.Stack.AddLast(fctx);
                var rv = engine.Process();
                Assert.AreEqual(
                    PType.BuiltIn.Int, rv.Type.ToBuiltIn(), "Result must be a ~Int");
                Assert.AreEqual(
                    expected,
                    (int) rv.Value,
                    "Fib(" + n + ") = " + expected + " and not " + (int) rv.Value);
            }
        }

        [DebuggerNonUserCode]
        private static int _fibonacci(int n)
        {
            return
                n <= 2
                    ?
                        1
                    : _fibonacci(n - 1) + _fibonacci(n - 2);
        }

        [Test]
        public void WhileLoop()
        {
            Compile(
                @"
var M;
function modify(x) =  M*x+12;

function main(newM, iterations)
{
    M = newM;
    var i = 0;
    var sum = 0;
    while(i<iterations)       
        sum = sum + modify(i++);
    return sum;
}
");

            var rnd = new Random();
            var m = rnd.Next(1, 13);
            var iterations = rnd.Next(3, 10);
            var sum = 0;
            for (var i = 0; i < iterations; i++)
                sum += m*i + 12;
            var expected = sum;

            ExpectNamed("main", expected, m, iterations);
        }

        [Test]
        public void ForLoop()
        {
            Compile(
                @"
var theList;

function getNextElement =
    if(static index < theList.Count)
        theList[index++]
    else 
        null;

function print(text) does static buffer.Append(text);

function main(aList, max)
{
    theList = aList;
    declare var print\static\buffer, getNextElement\static\index;
    print\static\buffer = new Text::StringBuilder;
    getNextElement\static\index = 0;
    
    var cnt = 0;
    for(var     elem;
        do      elem = getNextElement;
        until   elem == null
       )
    {
        var len = elem.Length;
        if(cnt + len > max)
            continue;
        print(elem);
        cnt += len;
    }
    return print\static\buffer.ToString;
}
");
            var buffer = new StringBuilder();
            const int max = 20;
            var aList = new List<string>(
                new[]
                    {
                        GenerateRandomString(5),
                        GenerateRandomString(10),
                        GenerateRandomString(15),
                        GenerateRandomString(3),
                        GenerateRandomString(5)
                    });

            foreach (string elem in aList)
                if (buffer.Length + elem.Length < max)
                    buffer.Append(elem);

            Expect(buffer.ToString(), engine.CreateNativePValue(aList), max);
        }

        [Test]
        public void StaticClrCalls()
        {
            Compile(
                @"
entry main;
function main(rawInteger)
{
    return System::Int32.Parse(rawInteger);
}
");
            var rnd = new Random();
            var expected = rnd.Next(1, 45);
            Expect(expected, expected.ToString());
        }

        [Test]
        public void Conditions()
        {
            Compile(
                @"
entry conditions;
var G;
function action1 does G += ""1"";
function action2 does G += ""2"";
function conditions(x,y)
{
    G = """";
    //Simple:
    if(x)
        action1;

    //Simple #2
    unless(y)
        {action1;}
    else
        action2;

    //Constant
    if(true and true)
        action1;
    else
        action2;

    //Complex
    if(x)
        unless (y)
            action1;
        else
            action2;
    else
    {
        action1;
        action2;
    }

    //Redundant blocks/conditions
    if(y){}else action2;
    
    if(not x){}else{}

    return G;
}
");
            const string tt = "1212";
            const string tx = "11112";
            const string xT = "2112";
            const string xx = "11122";

            Console.WriteLine("// TRUE  - TRUE ");
            Expect(tt, true, true);
            Console.WriteLine("// TRUE  - FALSE");
            Expect(tx, true, false);
            Console.WriteLine("// FALSE - TRUE ");
            Expect(xT, false, true);
            Console.WriteLine("// FALSE - FALSE");
            Expect(xx, false, false);
        }

        [Test]
        public void IndexAccess()
        {
            Compile(
                @"
declare function print;
function main(str, idx)
{
    var i = 0;
    
_while:
    unless(i < str.Length)
        goto _endwhile;
    print(str[i++] + "" "");
    goto _while;
_endwhile:
    return print(""--"" + str[idx]);    
}

function print(text) does
{
    if (static buffer == null) buffer = """";
    unless (text == null) buffer += text;
    return buffer;
}
");

            var str = Guid.NewGuid().ToString("N").Substring(0, 3);
            var rnd = new Random();
            var idx = rnd.Next(0, str.Length);
            var buffer = new StringBuilder();
            foreach (var ch in str)
                buffer.Append(ch.ToString() + ' ');
            buffer.Append("--" + str[idx]);
            var expect = buffer.ToString();
            Expect(expect, str, idx);
        }

        [Test]
        public void NonRecursiveTailCall()
        {
            options.RegisterCommands = true;
            Compile(
                @"
var buffer;
function print(text) = buffer.Append = text;
function work
{
    var args;
    buffer = new System::Text::StringBuilder(args[0]);
    print(args[1]);
    print(args[2]);
    return buffer;
}

function main(a,b,c) = work(a,b,c).ToString;
");
            var a = Guid.NewGuid().ToString("N");
            var b = Guid.NewGuid().ToString("N");
            var c = Guid.NewGuid().ToString("N");
            var expect = a + b + c;
            Expect(expect, a, b, c);
        }

        [Test]
        public void Commands()
        {
            options.RegisterCommands = true;
            engine.Commands.AddUserCommand(
                "conRev",
                new DelegatePCommand(
                    delegate(StackContext localSctx, PValue[] args)
                    {
                        var sb = new StringBuilder();
                        for (var i = args.Length - 1; i > -1; i--)
                            sb.Append(args[i].CallToString(localSctx));
                        return (PValue) sb.ToString();
                    }));

            var list =
                new[] {"the", "quick", "brown", "fox", "jumps", "over", "the", "lazy", "dog"};

            engine.Commands.AddUserCommand(
                "theList",
                new DelegatePCommand(
                    (localSctx, args) => localSctx.CreateNativePValue(list)));
            Compile(
                @"function main = conRev(theList[0], theList[1], theList[2], theList[3], theList[4], theList[5], theList[6], theList[7], theList[8]);");

            var buffer = new StringBuilder();
            for (var i = list.Length - 1; i > -1; i--)
                buffer.Append(list[i]);

            Expect(buffer.ToString());
        }

        public class SomeSortOfList : IEnumerable<string>
        {
            public SomeSortOfList(string input)
            {
                _input = input;
            }

            private readonly string _input;

            #region IEnumerable<string> Members

            public IEnumerator<string> GetEnumerator()
            {
                var words = _input.Split(new[] {' ', '\t', '\n', '\r'});

                foreach (var word in words)
                    if (word.Length > 0)
                        yield return word[0].ToString().ToUpperInvariant();

                yield return ">>";

                foreach (var word in words)
                    if (word.Length > 0)
                        if (word[0]%2 == 0)
                            yield return word.Insert(1, "\\").ToUpperInvariant();
                        else
                            yield return word.ToLowerInvariant();

                yield return "<<";
                yield return words.Length.ToString();
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion

            internal string _PrintList()
            {
                var buffer = new StringBuilder();
                foreach (var s in this)
                {
                    buffer.Append(' ');
                    buffer.Append(s);
                }
                return buffer.ToString();
            }

            internal int _CountList()
            {
                var e = GetEnumerator();
                while (e.MoveNext())
                    if (e.Current == ">>")
                        break;
                var cnt = 0;
                while (e.MoveNext())
                    if (e.Current == "<<")
                        break;
                    else
                        cnt += e.Current.Length;

                return cnt;
            }
        }

        [Test]
        public void SimpleForeach()
        {
            Compile(
                @"
function main(lst)
{
    var i = 0;
    foreach(var e in lst)
        i++;
    return i;
}
");
            Expect(5, PType.List.CreatePValue(new PValue[] {1, 2, 3, 4, 5}));
        }

        [Test]
        public void Foreach()
        {
            var lst = new SomeSortOfList("The quick brown fox jumps over the lazy dog");
            Compile(
                @"
var buffer;
function print does
    foreach( var arg in var args)
        buffer.Append(arg.ToString); 

function init does
    buffer = new System::Text::StringBuilder;

function printList(lst) does
{
    init;
    foreach( print("" "") in lst);
    return buffer.ToString;
}

function countList(lst) does
{
    var cnt = 0;
    var state = 0;
    foreach(var e in lst)
        if (state == 0)
            if (e == "">>"")
                state = 1;
            else 
                continue;
        else if (state == 1)
            if (e == ""<<"")
                state = 2;
            else
                cnt += e.Length;
        else
            continue;
    return cnt;
}
");

            ExpectNamed("printList", lst._PrintList(), sctx.CreateNativePValue(lst));
            ExpectNamed("countList", lst._CountList(), sctx.CreateNativePValue(lst));
        }

        [Test]
        public void GlobalVarInit()
        {
            Compile(
                @"
var buffer = new ::Text::StringBuilder;
var HW = ""Hello"";

var HW = HW + "" World"";

function print does foreach (buffer.Append in var args);

function main(x)
{
    if (x >= HW.Length) x = HW.Length -1;
    for (var i = 0; until i == x; i++)
    {
        HW = HW.Insert(i, i.ToString);
        print("">"", HW);
    }
    return buffer.ToString;
}
");
            var buffer = new StringBuilder();
            var hw = new StringBuilder("Hello World");
            var rnd = new Random();
            var x = rnd.Next(0, hw.Length + 1);
            var xi = x >= hw.Length ? hw.Length - 1 : x;
            for (var i = 0; i < xi; i++)
            {
                hw.Insert(i, i.ToString());
                buffer.Append(">");
                buffer.Append(hw.ToString());
            }
            var expect = buffer.ToString();

            Expect(expect, x);
        }

        [Test]
        public void PartialInitialization()
        {
            var ldr =
                Compile(
                    @"

Add System::Text to Import;

var buffer = new System::Text::StringBuilder;
function print does foreach( buffer.Append in var args);

var L1 = ""1o1"";
var L2;

function main(level)
{
    unless( level < 1)
       print(""#1="",L1,"";"");
    
    unless (level < 2)
       print(""#2="",L2,"";"");

    declare var L3;

    unless (level < 3)
        print(""#3="",L3,"";"");

    return buffer.ToString; 
}
");

            Expect("#1=1o1;", 1);

            //Continue compilation using the same loader
            Compile(
                ldr,
                @"
var L2 = ""2p2"";

{
    L1 = ""1o2"";
    buffer = new System::Text::StringBuilder;
}
");
            Expect("#1=1o2;#2=2p2;", 2);

            //Continue compilation using a different loader
            Compile(
                @"
var L3 = ""3z3"";
var L2 = ""2m3"";
var L1 = ""1k3"";

declare var buffer;
{ buffer = new System::Text::StringBuilder; }
");

            Expect("#1=1k3;#2=2m3;#3=3z3;", 3);
        }

        [Test]
        public void UselessBuildBlock()
        {
            var ldr = Compile(@"
    var myGlob; var initGlob;
");

            Compile(
                ldr,
                @"
build
{
    var loc = ""hello"";
    var kop = 55 * 77;
    var hup = loc.ToUpper + kop~String;
    
    declare var myGlob, initGlob;
    myGlob = hup.Substring(1);
    initGlob = 42;
}

var myGlob;
var initGlob = ""init"";

function main = myGlob + initGlob;
");
            Expect("ELLO" + (55*77) + "init");
        }

        [Test]
        public void References()
        {
            Compile(
                @"
function foldl(ref f, var left, var lst) does // (b -> a -> b) -> b -> a -> [b]
{
    foreach (var right in lst) left = f(left,right);
    return left;
}

function map(ref f, var lst) does // (a -> b) -> [a] -> [b]
{
    var nlst = new List;
    foreach(var e in lst) nlst.Add = f(e);
    return nlst;
}

var tuple\lst;
function tuple(x)
{
    static idx;
    declare tuple\lst as lst;

    if(idx == null)
        idx = 0;

    var ret = ~List.Create(x, lst[idx++]);
    unless(idx < lst.Count)
        idx = null;
    return ret;
}

ref reduce\f;
function reduce(x) = reduce\f(x[0], x[1]); // (a -> a -> b) -> (a,a) -> b

var chain;
function chained(x)
{
    foreach(ref f in chain) x = f(x);
    return x;
}

function add(left, right) = left + right; //a -> a -> a
function sub(left, right) = left - right;
function mul(left, right) = left * right;

function id(x) = x;                       //a -> a
function twice(x) = 2*x;
function binary(x) = 2^x;

function dummy(x) {}

function main()                           // IO() -> IO()
{
    //Create [1..10]
    var lst = new List;
    for(var i = 1; until i == 11; i++)
        lst.Add = i;
    
    var bin = map(->binary, lst); // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024
    var bin\sum = foldl(->add, 0, bin); // 2024

    chain = ~List.Create( -> twice, -> twice); //*4
    var bin\quad = map(->chained, bin); // 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096

    var twi = map(->twice, lst); // 2, 4, 6, 8, 10, 12, 14, 16, 18, 20

    tuple\lst = twi;
    var tup\bin_twi = map(->tuple, bin); // (2,2), (4,4), (6,8), (8,16), (10,32), (12,64), (14,128), (16,256), (18,512), (20,1024)

    ->reduce\f = ->sub;
    var tup\bin_twi\sub = map(->reduce, tup\bin_twi); // 0, 0, -2, -8, -22, -52, -114, -240, -494, -1004
    
    var tup\bin_twi\sub\sum = foldl(->add, 0 , tup\bin_twi\sub); // 1936
    
    var bin\quad\sum = foldl(->add, 0, bin\quad); // 8184

    return  (bin\quad\sum - tup\bin_twi\sub\sum)~Int; // 6248
}
");
            Expect(6248);
        }

        [Test]
        public void TypeIdentification()
        {
            Compile(
                @"
function main(arg)
{
    var r = """";
    if(arg is String)
        var r = arg + arg;
    else if(arg is List)
        foreach(var e in arg) var r += e;
    else if(arg is System::Text::StringBuilder)
        r = arg.ToString;

    return r;       
}
");
            string rs = GenerateRandomString(3);
            Expect(rs + rs, rs);

            var lst = new List<PValue>(
                new PValue[]
                    {
                        GenerateRandomString(2), GenerateRandomString(3),
                        GenerateRandomString(4)
                    });
            var ls = lst.Aggregate("", (current, e) => current + (e.Value as string));
            Expect(ls, (PValue) lst);

            var sb = new StringBuilder(GenerateRandomString(5));
            Expect(sb.ToString(), engine.CreateNativePValue(sb));
        }

        [Test]
        public void ClosureCreation()
        {
            Compile(
                @"
function clo1 = x => 2*x;

function clo2(a)
{
    return x => a*x;
}
");

            var rnd = new Random();

#if UseCil
            var pclo1 = GetReturnValueNamed("clo1");
            Assert.AreEqual(PType.Object[typeof(PFunction)], pclo1.Type);
            var clo1 = pclo1.Value as PFunction;
            Assert.IsNotNull(clo1);

            var pclo2 = GetReturnValueNamed("clo2", rnd.Next(1, 10));
            if (CompileToCil)
            {
                Assert.AreEqual(PType.Object[typeof(CilClosure)], pclo2.Type);
                var clo2 = pclo2.Value as CilClosure;
                Assert.IsNotNull(clo2);
                Assert.AreEqual(1, clo2.SharedVariables.Length);
            }
            else
            {
                Assert.AreEqual(PType.Object[typeof(Closure)], pclo2.Type);
                var clo2 = pclo2.Value as Closure;
                Assert.IsNotNull(clo2);
                Assert.AreEqual(1, clo2.SharedVariables.Length);
            }

#else
            PValue pclo1 = _getReturnValueNamed("clo1");
            Assert.AreEqual(PType.Object[typeof(Closure)], pclo1.Type);
            Closure clo1 = pclo1.Value as Closure;
            Assert.IsNotNull(clo1);
            Assert.AreEqual(0, clo1.SharedVariables.Length);

            PValue pclo2 = _getReturnValueNamed("clo2", rnd.Next(1, 10));
            Assert.AreEqual(PType.Object[typeof(Closure)], pclo2.Type);
            Closure clo2 = pclo2.Value as Closure;
            Assert.IsNotNull(clo2);
            Assert.AreEqual(1, clo2.SharedVariables.Length);
#endif
        }

        [Test]
        public void Lambda()
        {
            Compile(
                @"
function split(ref f, var lst, ref left, ref right)
{
    var l;
    var r;
    
    left = new List;
    right = new List;
    
    foreach(var x in lst)
    {
        f(x, ->l, ->r);
        left.Add = l;
        right.Add = r;
    }   
}

function splitter(ref f, ref g) = (x, ref left, ref right) => { left = f(x); right = g(x); };

function combine(ref f, left, right)
{
    var lst = new List;
    var max = left.Count;
    if(right.Count > max)
        max = right.Count;
    for(var i = 0; until i == max; i++)
        lst.Add = f(left[i], right[i]);
    return lst;
}

function main(lst)
{
    //Lambda expressions
    var twi = map( x => 2*x, lst);        
    var factors;
    var rests;
    //using splitter higher order function
    split( splitter( (x) => (x / 10)~Int, (x) => x mod 10 ), twi, ->factors, ->rests);
    var tuples = combine( (l,r) => ""("" + l + "","" + r + "")"", factors, rests);
    return foldl( (l,r) => l + "" "" + r, """", tuples);
}
");
            var lst = new int[10];
            var rnd = new Random();
            var sb = new StringBuilder();
            for (var i = 0; i < 10; i++)
            {
                lst[i] = rnd.Next(4, 49);
                var twi = 2*lst[i];
                var factors = twi/10;
                var rests = twi%10;
                sb.Append(" (" + factors + "," + rests + ")");
            }
            var expected = sb.ToString();

            var plst = lst.Select(x => (PValue) x).ToList();

            Expect(expected, PType.List.CreatePValue(plst));
        }

        [Test]
        public void Currying()
        {
            Compile(
                @"
function curry(ref f) = a => b => f(a,b);

function uncurry(ref f) = (a, b) => f(a).(b);

function map(ref f, lst)
{
    var nlst = new List;
    foreach( var x in lst)
        nlst.Add = f(x);
    return nlst;
}

function elementFeeder(lst) 
{
    var i = 0;
    var len = lst.Count;
    return () =>
    {
        if(i < len)
            return lst[i++];
        else
            return null;
    };
}

function listDifference(lst) does
{
    ref feed = elementFeeder(lst);
    return (x) => x - feed;
}

function main(lst, s)
{
    var add = (x,y) => x+y;
    ref additions = map(curry( add ),lst); // [ y => _const+y ]
    ref headComparer = uncurry(->listDifference);    
    var compared = map( x => headComparer(lst,x), additions(s) );
    var sb = new ::Text::StringBuilder;
    foreach(var e in compared) sb.Append("" "" + e);
    return sb.ToString;
}
");
            var rnd = new Random();
            var s = rnd.Next(2, 9);
            var plst = new List<PValue>();
            var head = -1;
            var sb = new StringBuilder();
            for (var i = 0; i < 10; i++)
            {
                var c = rnd.Next(11, 99);
                if (head < 0)
                    head = c;
                plst.Add(c);
                var d = c + s;
                var compared = d - head;
                sb.Append(" ");
                sb.Append(compared.ToString());
            }
            string expect = sb.ToString();

            Expect(expect, PType.List.CreatePValue(plst), s);
        }

        [Test]
        public void NestedFunctions()
        {
            Compile(
                @"
function apply(ref f, x) = f(x);

function main(p)
{
    function koo(x)
    {
        var q = x.ToString;
        if(q.Length > 1)
            return q + ""koo"";
        else
            return q;
    }
    
    if(p mod 10 == 0)
        function goo(x) = 2*x;
    else
        function goo(x) = 2+x;

    if(p > 50)
        function koo(x)
        {
            var q = x.ToString;
            q = q+q;
            if(q.Length mod 2 != 0)
                return q + ""q"";
            else
                return q;
        }

    return apply( ->koo , goo( p ) );
}
");
            var rnd = new Random();
            var ps =
                new[]
                    {
                        1, 2, 10, 27, 26, 57, 60, 157, rnd.Next(1, 190), rnd.Next(1, 190),
                        rnd.Next(1, 190)
                    };
            foreach (var p in ps)
            {
                int goo;
                if (p%10 == 0)
                    goo = 2*p;
                else
                    goo = 2 + p;

                string koo;
                var q = goo.ToString();
                if (p <= 50)
                    koo = q.Length > 1 ? q + "koo" : q;
                else
                {
                    q = q + q;
                    koo = q.Length%2 != 0 ? q + "q" : q;
                }

                Expect(koo, p);
            }
        }

        [Test]
        public void DeDereference()
        {
            Compile(
                @"
function applyInChain(var chain, var x)
{
    var y = x;
    foreach( ref f in chain)
        y = f(y);
    return y;
}

function createChain(ref t, var f, var g)
{
    t = new List;
    t[] = f;
    t[] = g;
}

function main(var m)
{
    ref flst; //Would be easier as { var flst; } but this is a test after all...
    createChain(->->flst, x => 2*x, x => 2+x);
    return applyInChain(->flst, m);
}
");
            var rnd = new Random();
            var m = rnd.Next(3, 500);
            var expected = 2 + (2*m);

            Expect(expected, m);
        }

        [Test]
        public void PowerSqrt()
        {
            Compile(@"
function main(x,y)
{
    return ::Math.Sqrt(x^2 + y^2);
}
");
            var x = 113.0;
            var y = 13.0;
            Expect(Math.Sqrt(Math.Pow(x,2)+Math.Pow(y,2)),x,y);
        }

        [Test]
        public void ExplicitIndirectCall()
        {
            Compile(
                @"
function map(f, lst)
{
    var nlst = new List;
    foreach( var e in lst)
        nlst[] = f.(e);
    return nlst;
}

function combine(flst, olst)
{
    var nlst = new List;
    for(var i = 0; until i == flst.Count; i++)
        nlst[] = flst[i].(olst[i]);
    return nlst;
}


function main(xlst, ylst)
{
    var dx = map( x => y => ::Math.Sqrt(x^2 + y^2), xlst);
    var d = combine( dx, ylst );
    var addition = x => y => x+y;
    var sum = 0;
    foreach(var e in d)
        sum = addition.(sum).(e);
    return sum;
}
");
            var rnd = new Random();
            var sum = 0.0;
            List<PValue> xlst = new List<PValue>(),
                         ylst = new List<PValue>();
            for (var i = 0; i < 10; i++)
            {
                var x = rnd.Next(3, 50);
                var y = rnd.Next(6, 34);

                xlst.Add(x);
                ylst.Add(y);

                double d = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                sum += d;
            }
            Expect(sum, PType.List.CreatePValue(xlst), PType.List.CreatePValue(ylst));
        }

        [Test]
        public void ConditionalExpression()
        {
            Compile(
                @"
function abs(x) = if(x > 0) x else -x;
function max(a,b) = if(a > b) a else b;
var rnd = new System::Random;
function randomImplementation(fa, fb) = (a,b) => if(rnd.Next(0,2) mod 2 == 0) fa.(a,b) else fb.(a,b);
function sum(lst)
{
    var s = 0;
    foreach(var e in lst)
        s += e;
    return s;
}

function main(lst, limit)
{
    ref min = randomImplementation((a,b) => if(max(a,b) == a) b else a, (a,b) => if(a < b) a else b);
    return -sum(all(map(a => min(a, limit), lst)));
}
");
            var rnd = new Random();
            var lst = new List<PValue>();
            var sum = 0;
            for (var i = 0; i < 10; i++)
            {
                var e = rnd.Next(-5, 6);
                lst.Add(e);
                if (e < 0)
                    sum += e;
            }

            lst.Add(4);
            lst.Add(1);
            lst.Add(0);
            lst.Add(-2);
            sum -= 2;
            Expect(-sum, PType.List.CreatePValue(lst));
        }

        [Test]
        public void NestedConditionalExpressions()
        {
            Compile(
                @"
function main(xs)
{
    var ys = [];
    foreach(var x in xs)
        ys[] = 
            if(x mod 2 == 0) if(x > 5)   x
                             else        x*2
            else             if(x < 10)  (x+1) / 2
                             else        x+2
        ;
    
    var s = 0;
    foreach(var y in ys)   
        s+=y;
    return s;
}
");
            var xs = new List<PValue>(
                new PValue[]
                    {
                        12, //=> 12
                        4, //=> 8
                        5, //=> 3
                        13 //=> 15
                    });

            Expect(12 + 8 + 3 + 15, PType.List.CreatePValue(xs));
        }

        [Test]
        public void GlobalRefAssignment()
        {
            Compile(
                @"
var theList;

function accessor(index) = v => 
{
    if(v != null)
        theList[index] = v;
    return theList[index];
};

ref first = accessor(0);
ref second = accessor(1);

function average(lst)
{
    var av = 0;
    foreach(var e in lst)
        av += e;
    return (av / lst.Count)~Int;
}

function main(lst)
{
    theList = lst;
    first = 4;
    second = 7;
    return ""f$first::$(average(lst))::s$second"";
}
");
            var lst = new List<PValue>();
            var av = 0;
            for (var i = 0; i < 10; i++)
            {
                lst.Add(i);
                var k = i != 0 ? i != 1 ? i : 7 : 4;
                av += k;
            }
            av = av/10;
            Expect("f4::" + av + "::s7", PType.List.CreatePValue(lst));
        }

        [Test]
        public void DataStructure()
        {
            Compile(
                @"
function chain(lst, serial)
{
    var c = new Structure;
    c.\(""IsSerial"") = if(serial != null) serial else true;
    c.\(""Functions"") = if(lst != null) lst else new List();
    function Invoke(this, prev)
    {
        var res = prev;
        if(this.IsSerial)
        {
            foreach(var f in this.Functions)
                prev = f.(prev);
            return prev;
        }
        else
        {
            var nlst = new List();
            foreach(var f in this.Functions)
                nlst[] = f.(prev);
            return nlst;
        }
    }

    c.\\(""Invoke"") = ->Invoke;
    c.\\(""IndirectCall"") = ->Invoke;
    return c;
}

function main(seed)
{
    function sqrt(x) = System::Math.Sqrt(x);
    ref ch = chain(~List.Create(
        x => x+2,
        x => x*2,
        x => x mod 3,
        x => (sqrt(x)*10)~Int,
        x => ""The answer is: "" + x
    ));

    return ch(seed);
}
");
            int seed = (new Random()).Next(400, 500);
            int expected = seed;
            expected = expected + 2;
            expected = expected*2;
            expected = expected%3;
            expected = (int) (Math.Sqrt(expected)*10);

            Expect("The answer is: " + expected, seed);
        }

        [Test]
        public void ListConcat()
        {
            Compile(
                @"

function foldl(ref f, var left, var lst)
{
    foreach(var x in lst)
        left = f(left, x);
    return left;   
}

function map(ref f, lst)
{
    var nlst = [];
    foreach(var x in lst) 
        nlst += f(x);
    return nlst;
}

function main(lst)
{
    var L2 = [];
    var last = null;
    foreach(var e in lst)
    {
        if(last == null)
        {
            last = e;
        }
        else
        {
            L2 += [[last, e]];
            last = null;
        }
    }
    
    function select(index) = map( pair => pair[index], L2 );
    function toString(obj) = foldl( (l,r) => l + r, """", obj);

    return toString( select(0) + select(1) );
}
");
            var lst = new List<PValue>();
            var sbo = new StringBuilder();
            var sbe = new StringBuilder();
            for (var i = 0; i < 10; i++)
            {
                lst.Add(i);
                if (i%2 == 0)
                    sbe.Append(i);
                else
                    sbo.Append(i);
            }
            Expect(
                string.Concat(sbe.ToString(), sbo.ToString()),
                new[] {PType.List.CreatePValue(lst)});
        }

        [Test]
        public void CoroutineSimple()
        {
            Compile(
                @"
function main(a,b,c)
{
    ref f = coroutine (x,y,z) => 
    { 
        yield x; 
        yield y; 
        yield z;
    } for (a,b,c);

    return f + f + f;
}
");
            Expect("abc", "a", "b", "c");
        }

        [Test]
        public void CoroutineFunction()
        {
            Compile(
                @"
function subrange(lst, index, count) does
    for(var i = index; i < index+count; i++)
        yield lst[i];

function main
{
    var f = coroutine -> subrange for ( var args, 2, 3 );
    var buffer = new System::Text::StringBuilder;
    foreach(var e in f)
        buffer.Append(e);
    return buffer.ToString;
}
");

            Expect("cde", "a", "b", "c", "d", "e", "f", "g");
        }

        [Test]
        public void CoroutineComplex()
        {
            Compile(
                @"
function map(ref f, var lst) = coroutine () =>
{
    foreach(var x in lst)
        yield f(x);
};

function where(ref predicate, var lst) = coroutine()=>
{
    foreach(var x in lst)
        if(predicate(x))
            yield x;
};

function limit(n, var lst) = coroutine() =>
{
    foreach(var x in lst)
        if(n-- > 0)
            yield x;
};

function skip(n, var lst) = coroutine() =>
{
    foreach(var x in lst)
        if(n-- <= 0)
            yield x;
};

function foldl(ref f, var left, var lst)
{
    foreach(var right in lst)
        left = f(left, right);
    return left;
}

function curry(ref f) = a => b => f(a,b);

function chain() does
var args; and return lst =>
{
    foreach(ref filter in var args)
        lst = filter(lst);
    return lst;
};

function main()
{
    function toString(lst) = foldl( (l,r) => l + r, """", lst);
    ref filterChain = chain(
        curry(->skip)   .(2),
        curry(->map)    .(x => 3*x),
        curry(->where)  .(x => x mod 2 == 0),
        curry(->limit)  .(3)
    );

    return toString(filterChain(var args));
}
");
            var lst = new List<PValue>();
            var buffer = new StringBuilder();
            var nums = 0;
            for (var i = 0; i < 20; i++)
            {
                lst.Add(i);
                if (i < 2)
                    continue;
                if (i*3%2 != 0)
                    continue;
                if (nums > 2)
                    continue;
                buffer.Append(i*3);
                nums++;
            }
            Expect(buffer.ToString(), lst.ToArray());
        }

        [Test]
        public void MapCommandImplementation()
        {
            Compile(
                @"
function my_map(ref f, var lst)
{
    var nlst = [];
    foreach(var x in lst)
        nlst[] = f(x);
    return nlst;
}

function foldl(ref f, var left, lst)
{
    foreach(var right in lst)
        left = f(left,right);
    return left;
}

function main()
{
    var args;
    var k = 1;
    var ver1 =    map( x => x + 1,   args);
    var ver2 = my_map( x => x + k++, args);

    ref y = coroutine() =>
    {
        foreach(var yy in ver1)
            yield yy;
    };

    var diff = map( x => x - y, ver2);
    return foldl( (l,r) => l + r, """",diff);
}
");

            Expect("01234", 1, 2, 3, 4, 5);
        }

        [Test]
        public void FoldLCommandImplementation()
        {
            Compile(
                @"
function my_foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left, right);
    return left;
}

function main()
{
    var sum = my_foldl( (l,r) => l + r, 0, var args) + 13;
    return foldl( (l,r) => l - r, sum, var args);
}
");

            Expect(13, 4, 5, 6, 7);
        }

        [Test]
        public void CallCommandImplementation()
        {
            Compile(
                @"
function sum()
{
    var s = 0;
    foreach(var x in var args)
        s += x~Int;
    return s;
}

function main()
{
    return call(->call, [->sum], var args);
}
");
            const int a = 3;
            const int b = 7;
            const int c = 9;
            const int d = 13;
            const int e = 14;
            const int f = 99;
            const int g = 101;

            Expect(
                a + b + c + d + e + f + g,
                PType.List.CreatePValue(new PValue[] {a, b, c}),
                PType.List.CreatePValue(new PValue[] {d, e}),
                PType.List.CreatePValue(new PValue[] {f}),
                PType.List.CreatePValue(new PValue[] {g}));
        }

        /// <summary>
        /// Makes sure that coroutines do not return an unnecessary null at the end.
        /// </summary>
        [Test]
        public void CoroutineNoNull()
        {
            Compile(
                @"
function main()
{
    var c = coroutine() => 
    {
        yield 0;
        yield 1;
        yield 2;
        yield 3;
    };

    var buffer = new System::Text::StringBuilder;
    foreach(var ce in c)
    {
        buffer.Append(""=>"");
        buffer.Append(ce);
        buffer.Append(""\n"");
    }

    return buffer.ToString;
}
");
            Expect("=>0\n=>1\n=>2\n=>3\n");
        }

        [Test]
        public void CoroutineRecursive()
        {
            Compile(
                @"
coroutine unfolded(lst)
{
    foreach(var x in lst)
        if(x is List || x is ::Prexonite::$Coroutine)
            foreach(var y in unfolded(x))
                yield y;
        else
            yield x;
}

function main()
{
    var args;
    var buffer = new System::Text::StringBuilder();
    foreach(var a in unfolded(args))
    {
        buffer.Append(a);
        buffer.Append(""."");
    }
    if(buffer.Length > 0)
        buffer.Length -= 1;
    return buffer.ToString;
}
");

            var args = new List<PValue>();
            var sub1 = new List<PValue>();
            var sub2 = new List<PValue>();
            var sub21 = new List<PValue>();

            args.Add(1);
            args.Add(2);
            args.Add(PType.List.CreatePValue(sub1));
            sub1.Add(3);
            sub1.Add(4);
            sub1.Add(5);
            args.Add(6);
            args.Add(PType.List.CreatePValue(sub2));
            sub2.Add(7);
            sub2.Add(PType.List.CreatePValue(sub21));
            sub21.Add(8);
            sub21.Add(9);
            sub21.Add(10);
            sub2.Add(11);
            sub2.Add(12);
            args.Add(13);
            args.Add(14);

            Expect("1.2.3.4.5.6.7.8.9.10.11.12.13.14", PType.List.CreatePValue(args));
        }

        [Test]
        public void CoroutineFib()
        {
            Compile(
                @"
var numbers = [];

declare function fib;

ref nextfib = coroutine() =>
{
    yield 1;
    yield 1;
    for(var i = 3; true; i++)
        yield fib(i-1) + fib(i-2);
};

function fib(n)
{
    while(numbers.Count < n)
        numbers[] = nextfib;

    return numbers[n-1];
}
");

            ExpectNamed("fib", _fibonacci(6), 6);
        }

        [Test]
        public void UnusedTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function main()
{
    var xs = [0];
    try
    {
        for(var i = 0; i < 5; i++)
            xs[] = i;
    }
    catch(var exc)
    {
        xs[] = exc;
    }
    finally
    {
        xs[] = ""--"";
    }

    return tos(xs);
}
");

            Expect("001234--");
        }

        [Test]
        public void UnusedSimpleTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function main()
{
    var xs = [0];
    try
    {
        for(var i = 0; i < 5; i++)
            xs[] = i;
    }
    xs[] = ""--"";

    return tos(xs);
}
");

            Expect("001234--");
        }

        [Test]
        public void IgnoreTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function main()
{
    var xs = [0];
    for(var i = 1; i < 6; i++)
        try
        {
            xs[] = i;
            if(i == 3)
                throw i; //Should be ignored
        }catch(var exc){}
    return tos(xs);
}
");

            Expect("012345");
        }

        [Test]
        public void FinallyTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

var xs = [0];
function main()
{
    for(var i = 1; i < 6; i++)
        try
        {
            if(i == 3)
                throw i;
        }
        finally
        {
            xs[] = i;
        }
    return tos(xs);
}
");
            try
            {
                Expect("012345");
            }
            catch (Exception exc)
            {
                Assert.AreEqual("3", exc.Message);
            }

            var pxs = target.Variables["xs"].Value;
            Assert.IsInstanceOfType(typeof(ListPType), pxs.Type, "xs must be a ~List.");
            var xs = (List<PValue>) pxs.Value;
            Assert.AreEqual("0", xs[0].CallToString(sctx));
            Assert.AreEqual("1", xs[1].CallToString(sctx));
            Assert.AreEqual("2", xs[2].CallToString(sctx));
            Assert.AreEqual("3", xs[3].CallToString(sctx));
        }

        [Test]
        public void CatchTry()
        {
            Compile(
                @"
function main()
{
    var xs = [0];
    for(var i = 1; i < 4; i++)
        try
        {
            if(i == 2)
                throw i;
            xs[] = i;
        }
        catch(var exc)
        {
            println(""(""+exc+"")"");
            xs[] = 2;
        }
    return xs.ToString;
}
");
            Expect("[ 0, 1, 2, 3 ]");
        }

        [Test]
        public void CatchFinallyTry()
        {
            Compile(
                @"
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function main()
{
    var xs = [0];
    for(var i = 1; i < 6; i++)
        try
        {
            if(i == 3)
                throw i; //Should be ignored
        }
        catch(var exc)
        {   
            xs[] = exc.Message;
        }
        finally
        {
            xs[] = i;
        }
    return tos(xs);
}
");
            Expect("0123345");
        }

        [Test]
        public void NestedTries()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function main()  [store_debug_implementation enabled;]
{
    var xs = [0];
    for(var i = 1; i < 6; i++)
        try
        {
            try
            {
                if(i mod 2 == 0)
                    throw i; //Should be ignored
            }
            catch(var exc)
            {
                if(exc.Message == ""4"")
                    throw exc;
            }
        }
        catch(var exc)
        {
            xs[] = i;
        }
        finally
        {
            xs[] = i;
        }
    return tos(xs);
}
");
            try
            {
                Expect("0123445");
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, exc);
            }
        }

        [Test]
        public void CrossFunctionTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function process(i) does
    if(i == 3)
        throw i;

function main()
{
    var xs = [0];
    for(var i = 1; i < 6; i++)
        try
        {
            process(i);
        }
        catch(var exc)
        {   
            xs[] = exc.Message;
        }
        finally
        {
            xs[] = i;
        }
    return tos(xs);
}
");
            Expect("0123345");
        }

        [Test]
        public void HandledSurfaceTry()
        {
            Compile(
                @"
function foldl(ref f, var left, var xs)
{
    foreach(var right in xs)
        left = f(left,right);
    return left;
}
function tos(xs) = foldl((a,b) => a + b,"""",xs);

function process(i) does try
{
    if(i == 3)
        throw i;
}
catch(var exc)
{
    throw exc.Message + ""i"";
}

function main()
{
    var xs = [0];
    for(var i = 1; i < 6; i++)
        try
        {
            process(i);
        }
        catch(var exc)
        {   
            xs[] = exc.Message;
        }
        finally
        {
            xs[] = i;
        }
    return tos(xs);
}
");
            Expect("01233i45");
        }

        [Test]
        public void Hashes()
        {
            Compile(
                @"
function mapToHash(ref f, xs)
{
    var h = {};
    foreach(var x in xs)
        h[] = x: f(x);
    return h;
}

coroutine reader(xs) does
    foreach(var x in xs)
        yield x;

function main()
{
    var h = mapToHash( x => (x+1)*2, var args);
    ref keys = reader(h.Keys);
    ref values = reader(h.Values);

    var diff = 0;
    for(var i = 0; i < h.Count; i++)
        diff += values - keys;
    return diff;
}
");
            var xs =
                new PValue[]
                    {
                        2, //4
                        3, //5
                        8, //10
                    };
            Expect(4 + 5 + 10, xs);
        }

        [Test]
        public void NestedFunctionCrossReference()
        {
            Compile(
                @"
function main()
{
    function A(xa)
    {
        return ""x"" + xa + ""x"";
    }

    function B(xb)
    {
        return ""b$(xb).$(->A)b"";
    }

    return B(var args[0]);
}
");

//#if UseCil
//            Expect("bs.CilClosure(function main\\A0( xa))b", "s");
//#else
//            Expect("bs.Closure(function main\\A0( xa))b", "s");
//#endif
            Expect("bs.function main\\A0(xa)b", "s");
        }

        [Test]
        public void CrossForeachTryCatch()
        {
            Compile(
                @"
coroutine mayFail
{
    yield 1;
    yield 2;
    throw ""I failed"";
    yield 3;
}

function main(sum)
{
    try
	{
        ref mightFail = mayFail;
		foreach(var sourceFile in [4,5,6])
		{
			sum+=sourceFile + mightFail;
		}
	}
	catch(var exc)
	{
        println(exc);
        sum*=2;
	}
	finally
	{
        sum*=10;        
	}
    return sum;
}
");
            Expect((1 + 4 + 2 + 5 + 1)*20, 1);
        }

        [Test]
        public void StructureToString()
        {
            Compile(
                @"
function main(x)
{
    var s = new Structure;
    s.\(""value"") = x;
    s.\\(""ToString"") = this => this.value;
    return s~String;
}
");
            Expect("xzzxy", "xzzxy");
        }

        [Test]
        public void UnbindCommandImplementation()
        {
            Compile(
                @"
function main()
{
    var buffer = new System::Text::StringBuilder;
    function print(s) does buffer.Append(s);
    var xs = [ 5,7,9,11,13,15 ];
    var fs = [];
    foreach(var x in xs)
    {
        fs[] = y => ""($(x)->$(y))"";
        unbind(->x);
        print(""$(x)."");
    }

    var i = 19;
    foreach(var f in fs)
        print(f.(i--));
    return buffer.ToString;
}
");

            const string expected = "5.7.9.11.13.15.(5->19)(7->18)(9->17)(11->16)(13->15)(15->14)";
            Expect(expected);
        }

        [Test]
        public void GlobalCode()
        {
            Compile(
                @"
var price = {};

{
    price[""apple""] = 3;
    price[""juice""] = 4;
    price[""pencil""] = 1;
}

//In a different file for example
{
    price[""apple""] *= 2;
}

function main(var lst)
{
    var sum = 0;
    foreach(var item in lst)
        sum += price[item.Key] * item.Value;
    return sum;
}
");
            var lst = new List<PValue>(4)
            {
                new PValueKeyValuePair("apple", 1),
                new PValueKeyValuePair("pencil", 5),
                new PValueKeyValuePair("juice", 2),
                new PValueKeyValuePair("apple", 2)
            };

            Expect(3*3*2 + 5*1 + 2*4, PType.List.CreatePValue(lst));
        }

        [Test]
        public void CoalescenceOperator()
        {
            Compile(
                @"
coroutine fetch(xs) does 
    foreach(var x in xs)
        yield x;

coroutine blit(xs, ys) does
    ref nextY = fetch(ys); and
    foreach(var x in xs)
        var y = nextY; and
        yield x ?? y ?? ""?"";

function main()
{
    var xs = [6,null,4,null,null,1];
    var ys = [1,2   ,3,4   ,null,6];
    return foldl((l,r) => l + ""."" + r, """", blit(xs,ys));
}        
");

            Expect(".6.2.4.4.?.1");
        }

        [Test]
        public void LoopExpressions()
        {
            Compile(
                @"
function main(s)
{
    var words = for(var i = 0; i < s.Length; i += 2)
                {
                    if(i == s.Length - 1)
                        yield s.Substring(i,1);
                    else
                        yield s.Substring(i,2);
                };
    
    return foldl    ( (l,r) => l + ""-"" + r, words.Count,  foreach(var word in words) 
                                                                yield word[0].ToUpper + word[1].ToLower;
                    );
}
");

            Expect("5-Bl-Oo-Dh-Ou-Nd", "BloodHound");
        }

        [Test]
        public void HarmlessTryFinally()
        {
            Compile(
                @"
function main
{
    var r;
    try
    {
        r = ""NO_ERROR"";
    }
    finally
    {
        r += "", REALLY"";
    }
    return r;
}
");
            Expect("NO_ERROR, REALLY");
        }

        [Test]
        public void TryCatchInFinally()
        {
            Compile(
                @"
function mightFail(x)
{
    throw ""I don't like $x."";
}

var sb = new System::Text::StringBuilder;

function print does foreach(sb.Append in var args);
function println does foreach(sb.AppendLine in var args);

function main()
{
    
    try
    {
        var xs = 
            foreach(var a in var args) 
                yield mightFail(a);
            ;
    }
    finally
    {
        xs = 
            foreach(var a in var args)
                yield ""NP($a)"";
            ;
    }
    catch(var exc)
    {
        print = ""EXC($(exc.Message))"";
    }

    print(foldl((l,r) => l + "" "" + r, "" BEGIN"", xs)); 
    return sb.ToString;
}
");

            var xs = new List<PValue> {4, "Hello", 3.4};

            Expect("EXC(I don't like 4.) BEGIN NP(4) NP(Hello) NP(3.4)", xs.ToArray());
        }

        [Test]
        public void LeftAppendArgument()
        {
            Compile(
                @"
coroutine where(ref f, xs) does foreach(var x in xs)
    if(f(x))
        yield x;

coroutine limit(max, xs) does
    var i = 0; and
    foreach(var x in xs)
        if(i++ >= max)
            break;
        else
            yield x;

coroutine skip(cnt, xs) does
    var i = 0; and
    foreach(var x in xs)
        if(i++ >= cnt)
            yield x;

coroutine map(ref f, xs) does
    foreach(var x in xs)
        yield f(x);

function main(sep) = foldl( (l,r) => $l + "" "" + $r, ""BEGIN"")
    << limit(3) << map( x => x.Length + sep + x ) << where( x => x.Length >= 3 ) << skip(1) << var args;
");

            Expect("BEGIN 3:abc 5:hello 3:123", ":", "ab", "abc", "hello", "12", "123", "8965");
        }

        [Test]
        public void RightAppendArgument()
        {
            Compile(
                @"
coroutine where(ref f, xs) does foreach(var x in xs)
    if(f(x))
        yield x;

coroutine limit(max, xs) does
    var i = 0; and
    foreach(var x in xs)
        if(i++ >= max)
            break;
        else
            yield x;

coroutine skip(cnt, xs) does
    var i = 0; and
    foreach(var x in xs)
        if(i++ >= cnt)
            yield x;

coroutine map(ref f, xs) does
    foreach(var x in xs)
        yield f(x);

function main(sep) = 
    var args >> 
    skip(1) >> 
    where( x => x.Length >= 3 ) >> 
    map( x => x.Length + sep + x ) >> 
    limit(3) >>
    foldl( (l,r) => $l + "" "" + $r, ""BEGIN"");
");

            Expect("BEGIN 3:abc 5:hello 3:123", ":", "ab", "abc", "hello", "12", "123", "8965");
        }

        [Test]
        public void ListSort()
        {
            Compile(
                @"
function main() = 
    [ ""f"", ""A"", ""x"", ""a"", ""h"", ""g"", ""H"", ""A"", ""f"", ""X"", ""F"", ""G"" ] >>
    sort
    ( 
        (a,b) =>
            a.ToLower.CompareTo(b.ToLower),
        (a,b) =>
            -(a.CompareTo(b))
    ) >>
    foldl( (l,r) => l + "","" + r, """");
");

            Expect(",A,A,a,F,f,f,G,g,H,h,X,x");
        }

        [Test]
        public void CompilerHook()
        {
            Compile(
                @"
//In some library
declare function debug;

Import
{
    System,
    Prexonite,
    Prexonite::Types,
    Prexonite::Compiler,
    Prexonite::Compiler::Ast
};

function ast(type) [is hidden;]
{
    var args;
    var targs = [];
    for(var i = 1; i < args.Count; i++)
        targs[] = args[i];

    return 
        asm(ldr.eng)
        .CreatePType(""Object(\""Prexonite.Compiler.Ast.Ast$(type)\"")"")
        .Construct((["""",-1,-1]+targs)~Object<""Prexonite.PValue[]"">)
        .self;
}

build does hook (t => 
{
    var body = t.Ast;
    if(t.Function.Id == ""main"")
    {
        //Append a return statement
        var ret = ast(""Return"", ::ReturnVariant.Exit);
        ret.Expression = ast(""GetSetMemberAccess"", ::PCall.Get, 
            ast(""GetSetSymbol"", ::PCall.Get, ""sb"", ::SymbolInterpretations.GlobalObjectVariable), ""ToString"");
        t.Ast.Add(ret);
    }

    function replace_debug(block)
    {
        for(var i = 0; i < block.Count; i++)
        {
            var stmt = block[i];
            if( stmt is ::AstGetSetSymbol && 
                stmt.Interpretation~Int == ::SymbolInterpretations.$Function~Int &&
                stmt.Id == ""debug"")
            {
                //Found a call to debug
                block[i] = ast(""AsmInstruction"", new ::Instruction(::OpCode.nop));
                for(var j = 0; j < stmt.Arguments.Count; j++)
                {
                    var arg = stmt.Arguments[j];
                    if(arg is ::AstGetSetSymbol)
                    {
                        var printlnCall = ast(""GetSetSymbol"", ::PCall.Get, ""println"", ::SymbolInterpretations.$Function);
                        var concatCall  = ast(""GetSetSymbol"", ::PCall.Get, ""concat"", ::SymbolInterpretations.$Command);
                        concatCall.Arguments.Add(ast(""Constant"",""DEBUG $(arg.Id) = ""));
                        concatCall.Arguments.Add(arg);
                        printlnCall.Arguments.Add(concatCall);

                        block.Insert(i,printlnCall);
                        i += 1;
                    }//end if                    
                }//end for

                //Recursively replace 'debug' in nested blocks.
                try
                {
                    foreach(var subBlock in stmt.Blocks)
                        replace_debug(subBlock);
                }
                catch(var exc)
                {
                    //ignore
                }//end catch
            }//end if
        }//end for
    }

    replace_debug(t.Ast);
});

//Emulation
var sb = new System::Text::StringBuilder;
function print(s)   does sb.Append(s);
function println(s) does sb.AppendLine(s);

//The main program
function main(a)
{
    var x = 3;
    var y = a;
    var z = 5*y+x;

    debug(x,y);
    debug(z);
}
");

            Expect("DEBUG x = 3\r\nDEBUG y = 4\r\nDEBUG z = 23\r\n", 4);
        }

        [Test]
        public void InitializationCodeHook()
        {
            Compile(
                @"
Imports { System, Prexonite, Prexonite::Types, Prexonite::Compiler, Prexonite::Compiler::Ast };

function ast(type) [is compiler;]
{
    var args;
    var targs = [];
    for(var i = 1; i < args.Count; i++)
        targs[] = args[i];

    return 
        asm(ldr.eng)
        .CreatePType(""Object(\""Prexonite.Compiler.Ast.Ast$(type)\"")"")
        .Construct((["""",-1,-1]+targs)~Object<""Prexonite.PValue[]"">)
        .self;
}

var SI [is compiler;] = null;
build {
    SI = new Structure;
    SI.\(""var"") = ::SymbolInterpretations.LocalObjectVariable;
    SI.\(""ref"") = ::SymbolInterpretations.LocalReferenceVariable;
    SI.\(""gvar"") = ::SymbolInterpretations.GlobalObjectVariable;
    SI.\(""gref"") = ::SymbolInterpretations.GlobalReferenceVariable;
    SI.\(""func"") = ::SymbolInterpretations.$Function;
    SI.\(""cmd"") = ::SymbolInterpretations.$Command;
    SI.\\(""eq"") = (this, l, r) => l~Int == r~Int;
    SI.\\(""is_lvar"") = (this, s) => s~Int == this.$var~Int;
    SI.\\(""is_lref"") = (this, s) => s~Int == this.$ref~Int;
    SI.\\(""is_gvar"") = (this, s) => s~Int == this.$gvar~Int;
    SI.\\(""is_gref"") = (this, s) => s~Int == this.$gref~Int;
    SI.\\(""is_func"") = (this, s) => s~Int == this.$func~Int;
    SI.\\(""is_cmd"") = (this, s) => s~Int == this.$cmd~Int;
    SI.\\(""is_obj"") = (this, s) => this.is_lvar(s) || this.is_gvar(s);
    SI.\\(""is_ref"") = (this, s) => this.is_lref(s) || this.is_gref(s);
    SI.\\(""is_global"") = (this, s) => this.is_gvar(s) || this.is_gref(s);
    SI.\\(""is_local"") = (this, s) => this.is_lvar(s) || this.is_lref(s);
    SI.\\(""make_global"") = (this, s) => 
        if(this.is_obj(s))
            this.gvar
        else if(this.is_ref(s))
            this.gref
        else
            throw ""$s cannot be made global."";            
    SI.\\(""make_local"") = (this, s) => 
        if(this.is_obj(s))
            this.lvar
        else if(this.is_ref(s))
            this.lref
        else
            throw ""$s cannot be made local."";
    SI.\\(""make_obj"") = (this, s) =>
        if(this.is_local(s))
            this.lvar
        else if(this.is_global(s))
            this.gvar
        else
            throw ""$s cannot be made object."";
    SI.\\(""make_ref"") = (this, s) =>
        if(this.is_local(s))
            this.lref
        else if(this.is_global(s))
            this.gref
        else
            throw ""$s cannot be made reference."";
}

build does hook(t => 
{
    //Promote local to global variables
    var init = t.$Function;

    if(init.Id != Prexonite::Application.InitializationId)
        return;

    var alreadyPromoted = foreach(var entry in init.Meta[""alreadyPromoted""].List)
                            yield entry.Text;
                          ;
    
    var toPromote = foreach(var loc in init.Variables)
                        unless(alreadyPromoted.Contains(loc))
                            yield loc;
                    ;
    
    foreach(var loc in toPromote)
    {
        loc = t.Symbols[loc];
        var glob = new ::SymbolEntry(SI.make_global(loc.Interpretation), loc.Id);
        t.Loader.Symbols[loc.Id] = glob;
        t.Loader.Options.TargetApplication.Variables[loc.Id] = new ::PVariable(loc.Id);
        var assignment = ast(""GetSetSymbol"", ::PCall.Set, glob.Id, glob.Interpretation);
        assignment.Arguments.Add(ast(""GetSetSymbol"", ::PCall.Get, loc.Id, loc.Interpretation));
        t.Ast.Add(assignment);        
        println(""Declared $glob"");
    }

    init.Meta[""alreadyPromoted""].AddToList(::MetaEntry.CreateArray(toPromote));
});

{
    var goo = 600;
}

{
    var goo2 = 780;
}

function main()
{
    return ""goo = $goo; goo2 = $goo2;"";
}
");
            Expect("goo = 600; goo2 = 780;");
        }

        [Test]
        public void CastAssign()
        {
            Compile(@"
function main(a,b,c)
{
    a~=Int;
    b ~ = Bool;
    c ~= String;

    return ((a+10)/5) + (if(b) c*a else c);
}
");

            const double a = 2.9;
            const int b = 27;
            const bool c = true;

            Expect("2TrueTrueTrue",a,b,c);
        }

        [Test]
        public void StoreBasic()
        {
            CompileStore(@"
function main(a,b,c)
{
    //string int bool
    var x = a.Substring(2);
    var y = b*5;
    var z = if(c) 
                ""x""   
            else 
                -1;    
    return ""$(x)$(y)$(z)"";
}
");

            Expect("cd50x","abcd",10,true);
        }

#if useIndex && false

        [Test]
        public void Index_Nested()
        {
            _compile(@"
function main(a,b)
{
    var s = ""+$a+"";
    
    function text(nt)
    {
        if(Not nt is Null)
            s = nt;
        return s;
    }

    function ToString
}
");
        }

#endif

        [Test]
        public void RotateIns()
        {
            Compile(@"
function main(a)
{   
    var s = new Structure;
    return s.\(""text"") = a;
}
");
            Expect("ham","ham");
        }

        private static int _fac(int n)
        {
            int r = 1;
            while (n > 1)
                r *= n--;
            return r;
        }

        [Test]
        public void DirectTailRecursion()
        {
            Compile(@"
function fac n r =
    if(n == 1)
        r
    else
        fac(n-1, n*r);
");

            ExpectNamed("fac",_fac(6),6,1);
        }

        [Test]
        public void IsNotSyntax()
        {
            Compile(@"
function main(a,b)
{
    if(a is not String)
        return a;
    else
        return b;
}
");

            Expect(125,125, "s-b-s");
            Expect(125.0, 125.0, "s-b-s");
            Expect(true, true, "s-b-s");
        }

        [Test]
        public void ReturnFromForeach()
        {
            Compile(@"
function main(xs)
{
    foreach(var x in xs)
        if(x > 5)
            return x;
    return -1;
}
");
            var xs = (PValue) new List<PValue> {1, 2, 3, 4, 7, 15};

            Expect(7,xs);
        }

        [Test]
        public void SuperFastPrintLn()
        {
            //Covers #10
            Compile(@"
function main = println;
");

            Expect("");
        }

        [Test]
        public void ReturnFromCatch()
        {
            Compile
                (@"

var lastCode = -1;
var buffer = new System::Text::StringBuilder;
function green f => f.();
var errors = [];
var ldrErrors = [""error1"",""error2""];

function main()
{
    try 
    {
        var exc = null;
        lastCode = buffer.ToString; //Save the code for error reporting            
    } 
    catch(exc)
    {
        //Exceptions are truly exceptional, so they should be printed
        // out right away.
        green = () =>
		{
			println(exc);
			exc = null;
			foreach(var err in errors)
				println(err);
		};
		return false;
    }
    finally
    {
        //Save errors for review and clean up.
        buffer.Length = 0;
        errors = ~List.CreateFromList(ldrErrors);            
    }
    println(errors.Count);
    return errors.Count == 0;
}");

            Expect(false);
        }

        [Test]
        public void MathPiWorksInCil()
        {
            Compile(@"
function main = pi;
");

            Expect(Math.PI);
        }

        [Test]
        public void ReverseCmd()
        {
            Compile(@"function main = [1,2,3,4,5] >> reverse >> foldl((a,b) => a + b,"""");");

            Expect("54321",new PValue[0]);
        }

        [Test]
        public void AsyncSeqSemantics()
        {
            Compile(@"
function main(xs)
{
    return foldl((a,b) => a + "">"" + b, """") << async_seq(xs);
}
");

            Expect(">1>2>3", (PValue) new List<PValue> {1, 2, 3});
        }

        [Test]
        public void MacroTemporaryAllocateFree()
        {
            Compile(@"
macro acquire_free()
{
    var v = context.AllocateTemporaryVariable;
    var node = new Prexonite::Compiler::Ast::AstConstant(""none"",-1,-1,v);
    context.FreeTemporaryVariable(v);
    return node;
}

function main = acquire_free;
");

            var mainFunc = target.Functions["main"];

            Assert.AreEqual(1,mainFunc.Variables.Count);
            var v = mainFunc.Variables.First();
            Expect(v);
        }

        [Test]
        public void CaptureUnmentionedMacroVariable()
        {
            Compile(@"
    macro echo() 
    {
        var f = (x) => 
            if(context is null)
                ""context is null""
            else
                context.LocalSymbols[x];
        println(f.(""x""));
    }

    function main()
    {
        var x = 15;
        echo;
        return x;
    }
");
            var clo = target.Functions["echo\\0"];
            Assert.IsNotNull(clo,"Closure must exist.");
            Assert.IsTrue(clo.Meta.ContainsKey(PFunction.SharedNamesKey));
            Assert.AreEqual(clo.Meta[PFunction.SharedNamesKey].List.Length, 1);
            Assert.AreEqual(clo.Meta[PFunction.SharedNamesKey].List[0].Text, MacroAliases.ContextAlias);

            Expect(15, new PValue[0]);
        }

        [Test]
        public void ForeachLastInConditionCil()
        {
            Compile(@"
function main(cond, xs)
{
    var z = 0;
    if(cond)
    {
        foreach(var x in xs)
            z += x;
    }
    else
    {
        z = 5;
    }
    return z;
}
");

            if(CompileToCil)
            {
                var main = target.Functions["main"];
                Assert.IsFalse(main.Meta[PFunction.VolatileKey],"main must not be volatile.");
                Assert.IsFalse(main.Meta.ContainsKey(PFunction.DeficiencyKey),"main must not have a deficiency");
                Assert.IsTrue(main.HasCilImplementation, "main must have CIL implementation.");
            }

            Expect(6, true, (PValue) new List<PValue> {1,2,3});
        }

        [Test]
        public void ReturnContinueFormTryFinally()
        {
            Compile(@"
function main()
{
    try
    {
        continue;
    } 
    finally
    {

    }
}
");

            var func = target.Functions["main"];
            
            var emptyArgV = new PValue[0];
            var emptyEnvironment = new PVariable[0];
           
            if(CompileToCil)
            {
                var nullContext = new NullContext(engine, target, new List<string>());
                Assert.IsTrue(func.HasCilImplementation, "main must have CIL implementation.");
                ReturnMode returnMode;
                PValue value;
                func.CilImplementation(
                    func, nullContext, emptyArgV, emptyEnvironment, out value, out returnMode);
                Assert.AreEqual(value.Type, PType.Null);
                Assert.AreEqual(returnMode, ReturnMode.Continue);
            }

            var fctx = func.CreateFunctionContext(engine, emptyArgV, emptyEnvironment);
            engine.Process(fctx);
            Assert.AreEqual(fctx.ReturnValue.Type, PType.Null);
            Assert.AreEqual(fctx.ReturnMode, ReturnMode.Continue);
        }

        [Test]
        public void JumpToAfterEmptyFinally()
        {
            Compile(@"
function main()
{
    try
    {
        goto after;
        goto fin;
    } 
    finally
    {
        fin:
    }
after:
}
");

            var func = target.Functions["main"];


            if (CompileToCil)
            {
                Assert.IsTrue(func.HasCilImplementation, "main must have CIL implementation.");
            }

            ExpectNull(new PValue[0]);
        }

        [Test]
        public void ConstantFoldingReferenceEquality()
        {
            Compile(@"
function interpreted [is volatile;] = System::Object.ReferenceEquals(""ab"", ""a"" + ""b"");
function compiled [is volatile;] = System::Object.ReferenceEquals(""ab"", ""a"" + ""b"");
");

            ExpectNamed("interpreted", true, new PValue[0]);
            ExpectNamed("compiled", true, new PValue[0]);
        }

        [Test]
        public void UnlessConditionalExpression()
        {
            Compile(@"
function main()
{
    return unless(true) 1 else 2;
}
");

            Expect(2,new PValue[0]);

        }

        [Test]
        public void BuildBlockDoesNotTriggerInitialization()
        {
            Compile(@"
var flag = true;

function write does print(var args);

build does write(""nothing"");
");

            Assert.IsNull(target.Variables["flag"].Value.Value);
        }

        [Test]
        public void NestedVariableShadowing()
        {
            Compile(@"
function main(x,y)
{
    var a = x;
    function innerShadow
    {
        new var a = y; //variable is new-declared, it should not capture the outer variable
        return a;
    }
    function innerCapture
    {
        a = y;
        return a;
    }

    var t1 = a;
    var k1 = innerShadow;
    var t2 = a;
    var k2 = innerCapture;
    var t3 = a;

    return ""$t1,$k1; $t2,$k2; $t3"";
}
");

            Expect("x,y; x,y; y","x","y");
        }

        [Test]
        public void DeclareNewVarTopLevel()
        {
            Compile(
                @"
function main()
{
    var buffer = new System::Text::StringBuilder;
    function print(s) does buffer.Append(s);
    new var xs = [ 5,7,9,11,13,15 ];
    var fs = [];
    foreach(var x in xs)
    {
        fs[] = y => ""($(x)->$(y))"";
        print(""$(new var x)."");
    }

    var i = 19;
    foreach(var f in fs)
        print(f.(i--));
    return buffer.ToString;
}
");

            const string expected = "5.7.9.11.13.15.(5->19)(7->18)(9->17)(11->16)(13->15)(15->14)";
            Expect(expected);
        }

        [Test]
        public void ObjectCreationFallback()
        {
            Compile(@"
declare function make_foo as create_foo;

function main(x,y)
{
    var a = new foo(x);
    function create_bar(z) = ""bar($z)"";
    var b = new bar(y);
    return a + b;
}

function make_foo(z) = ""foo($z)"";
");

            Expect("foo(x)bar(y)", "x", "y");
        }

        [Test] //#19
        public void ObjectIdentity()
        {
            Compile(@"
function eq(x) = x == x;
function neq(x) = x != x;
");

            ExpectNamed("eq", true, new PValue(new object(), PType.Object[typeof(object)]));
            ExpectNamed("neq",false, new PValue(new object(), PType.Object[typeof(object)]));
        }

        [Test] //#18
        public void HexEscapeSequences()
        {
            Compile(@"
function main = ""\x20\x21\x9\x0020\x020\xAAAA\uABCD\U0000ABCD"".ToCharArray() >> map(x => x~Int) >> all;
");

            Expect(new List<PValue>{0x20, 0x21, 0x9, 0x0020, 0x020, 0xAAAA, 0xABCD, 0x0000ABCD});
        }

        [Test]
        public void InnerFunctionNamespaceImport()
        {
            Compile(@"
Import {
    System,
    Prexonite
};

function main(x)[ Add Prexonite::Types to Import; ]
{
    function inner  k => k is ::PValueKeyValuePair;
    ref lambda =    k => k is ::PValueKeyValuePair;
    return inner(x) and lambda(x);
}
");

            Expect(true, new PValueKeyValuePair(1,2));
            Expect(false, 1);
        }

        [Test]
        public void ConditionalExpressionVsKvpPriority()
        {
            Compile(@"
function main(x,y,z) = if(x) x else y:z;
");

            Expect(true, true, 1, 2);
        }

        [Test]
        public void KvpSelfPriority()
        {
            Compile(@"
function main(x,y,z) = (x : y : z).Key;
");

            Expect(1,1,2,3);
        }

        private class Callable : IIndirectCall
        {
            private readonly Func<StackContext, PValue[], PValue> _impl;

            public Callable(Func<StackContext, PValue[], PValue> impl)
            {
                if (impl == null)
                    throw new ArgumentNullException("impl");
                _impl = impl;
            }

            #region Implementation of IIndirectCall


            public PValue IndirectCall(StackContext sctx, PValue[] args)
            {
                return _impl(sctx, args);
            }

            #endregion
        }

        [Test]
        public void ReturnModes()
        {
            Compile(@"
function ret_exit()
{
    return 5;
}

function ret_yield()
{
    yield 6;
}

function ret_continue()
{
    continue;
}

function ret_break()
{
    break;
}
");

            _testReturnMode("ret_exit",ReturnMode.Exit,5);
            _testReturnMode("ret_yield", ReturnMode.Continue, 6);
            _testReturnMode("ret_continue",ReturnMode.Continue, PType.Null);
            _testReturnMode("ret_break",ReturnMode.Break, PType.Null);
        }

        private void _testReturnMode(string id, ReturnMode mode, PValue retVal)
        {
            var fctx = target.Functions[id].CreateFunctionContext(engine);
            engine.Process(fctx);
            Assert.AreEqual(fctx.ReturnMode, mode, "Return mode for function " + id + " does not match.");
            Assert.IsTrue((bool) retVal.Equality(fctx, fctx.ReturnValue).Value, "Return value for function " + id + " does not match.");
        }

        [Test]
        public void ReturnFromFinally()
        {
            Compile(@"
var t = """";
function trace x = t += x;

function main(x)
{
    try {
        trace(""t"");
    } finally {
        if(x)
            yield t;
        else 
            trace(""n"");
    }

    return t;
}
");

            var mainTable = target.Functions["main"].Meta;

            if (CompileToCil)
            {
                Assert.IsTrue(mainTable[PFunction.VolatileKey].Switch, "return from finally is illegal in CIL");
                Assert.IsTrue(mainTable[PFunction.DeficiencyKey].Text.Contains("SEH"),
                              "deficiency must be related to SEH.");
            }
            Expect("tn",false);
        }

        [Test]
        public void FunctionCompositionSyntax()
        {
            Compile(@"
function closed(x,y) 
{   
    var f = x then y;
    return f.(null);
}

function partialLeft(x,y) 
{   
    var f = (? then y);
    f = f.(x);
    return f.(null);
}

function partialRight(x,y) 
{   
    var f = (x then ?);
    f = f.(y);
    return f.(null);
}

function partialFull(x,y) 
{   
    var f = (? then ?);
    f = f.(x,y);
    return f.(null);
}

function chainedPrio(x,y,z) 
{   
    var f = x then y then z;
    return f.(null);
}
");

            var x =
                sctx.CreateNativePValue(
                    new Callable((stackContext, args) => "x" + args[0].CallToString(stackContext) + "x"));
            var y =
                sctx.CreateNativePValue(
                    new Callable((stackContext, args) => "y" + args[0].CallToString(stackContext) + "y"));
            var z =
                sctx.CreateNativePValue(
                    new Callable((stackContext, args) => "z" + args[0].CallToString(stackContext) + "z"));

            ExpectNamed("closed","yxxy",x,y);
            ExpectNamed("partialLeft", "yxxy", x, y);
            ExpectNamed("partialRight", "yxxy", x, y);
            ExpectNamed("partialFull", "yxxy", x, y);
            ExpectNamed("chainedPrio", "zyxxyz", x, y, z);
        }

        [Test]
        public void PartialInitialization2()
        {
            var ldr = Compile(@"
var x = 5;

function main(y)
{
    return y + x;
}
");

            Expect(11, 6);

            Compile(ldr, @"
var x = 17;
var z = 9;

function main2(x)
{
    return z + main(x);
}
");

            ExpectNamed("main2",20+9, 3);

            Compile(ldr, @"
var x = 22;
var z = 20;
");

            ExpectNamed("main2",20+22+4,4);
        }

        [Test]
        public void ArgsFallback()
        {
            Compile(@"
function main(args)
{
    foreach(var arg in var \args)
        args += arg;

    return args;
}
");
            //note that the first argument gets added twice!
            Expect("22abcdef","2", "a","b","c","d","e","f");
        }

        [Test]
        public void ParamDefaultNull()
        {
            Compile(@"
function main(x,y)
{
    return y;
}
");

            ExpectNull("main","z");
        }

        [Test]
        public void VariableDefaultNull()
        {
            Compile(@"
function main(x)
{
    var y;
    return y;
}");

            ExpectNull("main", "z");
        }

        [Test]
        public void LocalRef()
        {
            Compile(
                @"
function interpolate(x,y,t, ref result)
{
    if(y < x)
        interpolate(y,x,t,->result);
    else
        result = x+(y-x)*t;
}

function main(x,t)
{
    var y = x*1.5;
    interpolate(y,x,t,->y);
    return y;
}
");

            var x = 22.5;
            var y = x*1.5;
            var t = 0.75;

            Expect((x+(y-x)*t),x,t);
        }

        [Test]
        public void GlobalRef()
        {
            Compile(
                @"
var result;

function interpolate(x,y,t, ref result)
{
    if(y < x)
        interpolate(y,x,t,->result);
    else
        result = x+(y-x)*t;
}

function main(x,t)
{
    var y = x*1.5;
    interpolate(y,x,t,result = ?);
    return result;
}
");

            var x = 22.5;
            var y = x*1.5;
            var t = 0.75;

            Expect((x+(y-x)*t),x,t);
        }

        [Test]
        public void RealArithmetic()
        {
            Compile(
                @"function main(x)
{
    var y = x * 2.5;
    var z = y / 1.4;
    var a = z^y;
    return a;
}");

            var x = Math.PI;
            var y = x*2.5;
            var z = y/1.4;
            var a = Math.Pow(z, y);
            Expect(a,x);
        }

        [Test]
        public void AsmLdrApp()
        {
            Compile(@"
function foo(x) = 2*x;
function main(x)
{
    return asm(ldr.app).Functions[""foo""].(x);
}");
            Expect(4,2);
        }

        [Test]
        public void DynamicTypeIsArray()
        {
            Compile(@"
function main(x,type)
{
    return x is Object<(type + ""[]"")>;
}
");
            Expect(false, 4,"System.String");
            Expect(true, sctx.CreateNativePValue(new int[]{1,2,3}),"System.Int32");
        }

        [Test]
        public void PostIncDecGlobal()
        {
            Compile(@"
var i = 0;
function main(x)
{
    if(x mod 2 == 0)
        return i++;
    else
        return i--;
}
");
            var i = 0;
            Expect(i++,2);
            Expect(i++, 4);
            Expect(i--,3);
            Expect(i++, -2);
        }

        [Test]
        public void PreIncDecGlobal()
        {
            Compile(@"
var i = 0;
function main(x)
{
    if(x mod 2 == 0)
        return ++i;
    else
        return --i;
}
");
            var i = 0;
            Expect(++i,2);
            Expect(++i, 4);
            Expect(--i, 3);
            Expect(++i, 4);
        }

        [Test]
        public void StaticSet()
        {
            engine.RegisterAssembly(typeof (StaticClassMock).Assembly);

            Compile(
                @"
function main(y,x)
[ Add Prx::Tests to Import; ]
{
    for(var i = 0; i < y; i++)
    {
        ::StaticClassMock.SomeProperty = x;
    }
    if(::StaticClassMock.SomeProperty is not null)
        return ::StaticClassMock.SomeProperty;
    else
        return 5;
}
");
            var x = "500";
            Expect(x,10,x);
        }

        [Test]
        public void BreakFromProtected()
        {
            Compile(
                @"
var t = """";
function trace x=t+=x;
function main()
{
    try{
        trace(""t"");
        break;
    }catch(var exc) {
        trace(""c"");
    }
    
    return t;
}
");

            ExpectNull();
            Assert.IsTrue((bool)((PValue)"t").Equality(sctx,target.Variables["t"].Value).Value,"trace does not match");
        }

        [Test]
        public void TryAsLastStatement()
        {
            Compile(
                @"
var t;
function main(x)
{
    try {
        t = ""t"";  
    } finally {
        t += ""f"";
    }
}");

            ExpectNull();
            Assert.IsTrue((bool)target.Variables["t"].Value.Equality(sctx,"tf").Value, "Unexpected trace");
        }

        [Test]
        public void BitwiseOperators()
        {
            Compile(@"
function main(x,y,z)
{
    var a = x | y | z;
    var b = x & y;
    var c = y & z;
    var d = x & y & z;
    var e = x xor y;
    var f = x & y | z;
    var g = x | y & z;
    return [a,b,c,d,e,f,g];
}
");

            var x = 27;
            var y = 0x113;
            var z = 0x0FFFA;

            var a = x | y | z;
            var b = x & y;
            var c = y & z;
            var d = x & y & z;
            var e = x ^ y;
            var f = x & y | z;
            var g = x | y & z;

            Expect(new List<PValue>{a,b,c,d,e,f,g}, x,y,z);
        }

        [Test]
        public void EndFinallies()
        {
            Compile(
                @"
var t = """";
function trace x=t+=x;
function main(x,y)
{
    try {
        trace(""t1"");
    } finally {
        goto endfinally1;
        trace(""f1"");
endfinally1:
    }

    trace(""e1"");

    try {
        trace(""t2"");
    } finally {
        if(x)
            goto endfinally2;    
        trace(""f2"");
endfinally2:
    }

    trace(""e2"");
    try {
        trace(""t3"");
    } finally {
        if(not y)
            goto endfinally3;    
        trace(""f3"");
endfinally3:
    }

    trace(""e3"");

    return t;
}
");
            if (CompileToCil)
            {
                Assert.IsNotNull(target.Functions["main"],"function main must exist.");
                Assert.IsFalse(target.Functions["main"].Meta[PFunction.VolatileKey].Switch, 
                    "should compile to cil successfully");
            }
            Expect("t1e1t2e2t3e3",true,false);
        }

        [Test]
        public void LazyAndOptimization()
        {
            Compile(@"
var x = true;
var y = false;

function main(z)
{
    var a = x and 1;
    var b = x and 0;
    var c = true and 1;
    var d = true and 0;
    var e = y and 1;
    var f = y and 0;
    var g = false and 1;
    var h = false and 0;
    var i = x and z;
    var j = y and z;
    var k = true and z;
    var l = false and z;
    
    var s = ""$(a)$(b)$(c)$(d)$(e)$(f)$(g)$(h)$(i)$(j)$(k)$(l)"";

    foreach(var p in [a,b,c,d,e,f,g,h,i,j,k,l])
        if(p is not Bool)
        {
            s += "" Detected non-Bool value"";
            break;
        }

    return s;
}");

            const string prefix = "TrueFalseTrueFalseFalseFalseFalseFalse";
            const string valueEqTrue = "TrueFalseTrueFalse";
            const string valueEqFalse = "FalseFalseFalseFalse";

            Expect(prefix + valueEqTrue,true);
            Expect(prefix + valueEqFalse, false);
            Expect(prefix + valueEqTrue,6);
            Expect(prefix + valueEqFalse, 0);
            Expect(prefix + valueEqFalse, PType.Null);
        }

        [Test]
        public void LazyOrOptimization()
        {
            Compile(@"
var x = true;
var y = false;

function main(z)
{
    var a = x or 1;
    var b = x or 0;
    var c = true or 1;
    var d = true or 0;
    var e = y or 1;
    var f = y or 0;
    var g = false or 1;
    var h = false or 0;
    var i = x or z;
    var j = y or z;
    var k = true or z;
    var l = false or z;
    
    var s = ""$(a)$(b)$(c)$(d)$(e)$(f)$(g)$(h)$(i)$(j)$(k)$(l)"";

    foreach(var p in [a,b,c,d,e,f,g,h,i,j,k,l])
        if(p is not Bool)
        {
            s += "" Detected non-Bool value"";
            break;
        }

    return s;
}");

            const string prefix = "TrueTrueTrueTrueTrueFalseTrueFalse";
            const string valueEqTrue = "TrueTrueTrueTrue";
            const string valueEqFalse = "TrueFalseTrueFalse";

            Expect(prefix + valueEqTrue, true);
            Expect(prefix + valueEqFalse, false);
            Expect(prefix + valueEqTrue, 6);
            Expect(prefix + valueEqFalse, 0);
            Expect(prefix + valueEqFalse, PType.Null);
        }

        [Test]
        public void StringEscapeCollision()
        {
            Compile(@"
function main(s)
{
    var es = s.Escape;
    var ues = es.Unescape;
    return ""$s:$ues:$es"";
}
");

            //� = U+00E4

            //Simple
            _expectRoundtrip("X�x", "X\\xE4x");

            //Collision
            _expectRoundtrip("A�E0", "A\\u00E4E0");
        }

        private void _expectRoundtrip(string text, string escaped)
        {
            Expect(string.Format("{0}:{0}:{1}", text, escaped),text);
        }

        [Test]
        public void NullStringEscapeSequence()
        {
            Compile(@"
function main(x,y)
{
    var z = x;
    var z\ = y;
    var z\t = z\;

    return ""$z\&_$z\t;$z&:"" + ""\&"".Length;
}

function main_vs(x,y)
{
    var z = x;
    var z\ = y;
    var z\t = z\;

    return @""$z\&_$z\t;$z&:"" + ""\&"".Length;
}

function unharmed(x,y)
{
    var z\ = x == y;
    return z\&&true;
}
");

            const string expected = "A_B;A&:0";
            const string x = "A";
            const string y = "B";
            Expect(expected,x,y);
            ExpectNamed("main_vs",expected,x,y);
            ExpectNamed("unharmed",true,x,x);
            ExpectNamed("unharmed",false,x,y);
        }

        [Test]
        public void SingleQuotes()
        {
            Compile(@"
function al'gebra_f(x'') = x'' + 6'000'';

function main(x,x')
{
    var al'gebra = al'gebra_f(x');
    return ""$x $al'gebra $x':"" + 54'08.9;
}
");

            Expect("A 7000 1000:5408.9","A",1000);
        }

        

        [Test]
        public void ObjectCreationOptimizeReorder()
        {
            engine.RegisterAssembly(typeof(StaticClassMock).Assembly);
            Compile(@"
function main()
{
    var x = ""xXx"";
    var obj = new Prx::Tests::ConstructEcho(-1,x);
    println(obj);
    return obj.ToString;
}
");

            Expect("-1-xXx");
        }

        [Test]
        public void DuplicatingJustEffectBlockExpression()
        {
            var ldr = Compile(@"
var s;
function main()[is volatile;]
{
    s = ""BEGIN--"";
}
");
            var ct = ldr.FunctionTargets["main"];
            ct.Function.Code.RemoveAt(ct.Function.Code.Count-1);
            var block = new AstBlockExpression("file", -1, -2);

            var assignStmt = new AstGetSetSymbol("file", -1, -2, PCall.Set,
                                                                       "s",
                                                                       SymbolInterpretations.
                                                                           GlobalObjectVariable);
            assignStmt.Arguments.Add(new AstConstant("file",-1,-2,"stmt."));
            var incStmt = new AstModifyingAssignment("file", -1, -2,
                                                                            BinaryOperator.Addition,
                                                                            assignStmt,
                                                                            SymbolInterpretations.
                                                                                Command,
                                                                            Prexonite.Commands.Core.
                                                                                Operators.Addition.
                                                                                DefaultAlias);

            var assignExpr = new AstGetSetSymbol("file", -1, -2, PCall.Set,
                                                                       "s",
                                                                       SymbolInterpretations.
                                                                           GlobalObjectVariable);
            assignExpr.Arguments.Add(new AstConstant("file", -1, -2, "expr."));
            var incExpr = new AstModifyingAssignment("file", -1, -2,
                                                                            BinaryOperator.Addition,
                                                                            assignExpr,
                                                                            SymbolInterpretations.
                                                                                Command,
                                                                            Prexonite.Commands.Core.
                                                                                Operators.Addition.
                                                                                DefaultAlias);

            block.Statements.Add(incStmt);
            block.Expression = incExpr;

            var effect = block as IAstEffect;
            Assert.IsNotNull(effect);
            effect.EmitEffectCode(ct);

            var sourcePosition = new SourcePosition("file", -1, -2);
            ct.EmitLoadGlobal(sourcePosition, "s");
            ct.Emit(sourcePosition, OpCode.ret_value);

            if (CompileToCil)
                Prexonite.Compiler.Cil.Compiler.Compile(ldr, target, StaticLinking);

            Expect("BEGIN--stmt.expr.");
        }

        #region Helper

        #endregion
    }
}

namespace Prx.Tests
{
    public static class StaticClassMock
    {
        public static string SomeProperty { get; set; }
    }

    public class ConstructEcho
    {
        public int Index { get; set; }
        public string X { get; set; }

        public ConstructEcho(int index, string x)
        {
            Index = index;
            X = x;
        }

        public override string ToString()
        {
            return string.Format("{0}-{1}", Index, X);
        }
    }
}