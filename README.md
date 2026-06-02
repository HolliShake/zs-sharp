# obiwan

> A lightweight scripting language and interpreter implemented in C# (.NET 8), designed for embedding, scripting, and
> experimenting with language design.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Language](https://img.shields.io/badge/language-C%23-239120?style=flat-square&logo=csharp)](https://github.com/HolliShake/zs-sharp)
[![AOT](https://img.shields.io/badge/publish-AOT-blue?style=flat-square)](https://github.com/HolliShake/zs-sharp/blob/main/obiwan.csproj)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)
[![Commits](https://img.shields.io/badge/commits-41-orange?style=flat-square)](https://github.com/HolliShake/zs-sharp/commits/main)

---

## Features

- **Async functions** with `await` — coroutine-style async execution
- **Promise chaining** via `->then()` and `->error()` on any async call
- **First-class functions** — anonymous functions (`fn(...)`) as values and arguments
- **Destructuring** — array and object unpacking into local bindings
- **Switch expressions** — pattern-matching expressions that yield a value
- **Error handling** — `try`/`catch` blocks, including across `await` boundaries
- **Data types** — arrays `[...]`, objects `{...}`, numbers, strings, booleans
- **Loops** — `while` loops with full expression support
- **Closures** — functions capture their enclosing scope
- **Embeddable** — clean C# core designed to be hosted in other applications

---

## Example

```obiwan
fn add(a, b) async {
    return a + b;
}

// Promise chaining with ->then() and ->error()
add(0, 1)
    ->then(fn(data) {
        return data + 1;
    })
    ->then(fn(data) {
        return data * 2;
    })
    ->then(cb);

add(0, "foccer")
    ->error(fn(data) {
        print "THEN";
    })
    ->error(cbError);
```

### Try/Catch across async boundaries

```obiwan
fn willThrow() async {
    2 + "focc!";
}

fn caller() async {
    try {
        await willThrow();
    } catch (err) {
        print "caught:", err;
    }
}

caller();
```

### Switch expression

```obiwan
println(switch (val) {
    0 => "zero",
    1 => "one",
    _ => "other"
});
```

### Destructuring

```obiwan
const returnArray = fn() {
    return [1, 2];
};

var [x, y] = returnArray();

local { a: b, c: d } = { a: "Dog", c: "Cat" };
print b, d;
```

---

## Usage

Build and run with .NET 8:

```sh
dotnet build
dotnet run -- -r path/to/script.obi
```

### CLI Options

| Flag               | Description                 |
|--------------------|-----------------------------|
| `-r, --run <path>` | Run an obiwan source file   |
| `-t, --test`       | Run the internal test suite |
| `-h, --help`       | Show help                   |

---

## Build & Publish

To publish a self-contained binary (AOT-compiled):

```sh
dotnet publish -c Release
```

> AOT publishing is enabled via `<PublishAot>true</PublishAot>` in the project file.

---

## Project Structure

The interpreter is implemented as a classic pipeline:

```
Source (.obi)
    │
    ▼
Lexer.cs          — tokenizes source into a stream of Token/TokenType
    │
    ▼
Parser.cs         — builds an AST (Ast.cs / AstType.cs)
    │
    ▼
Compiler.cs       — emits bytecode (OpCode.cs / Code.cs)
    │
    ▼
Vm.cs             — executes bytecode via stack frames (Frame.cs)
```

### All source files

| File                                                      | Role                             |
|-----------------------------------------------------------|----------------------------------|
| `Program.cs`                                              | CLI entry point                  |
| `Lexer.cs`                                                | Tokenizer                        |
| `Token.cs`, `TokenType.cs`                                | Token model                      |
| `Parser.cs`                                               | AST construction                 |
| `Ast.cs`, `AstType.cs`                                    | AST node types                   |
| `Compiler.cs`                                             | Bytecode emitter                 |
| `OpCode.cs`, `OpCodeDebug.cs`                             | Instruction set                  |
| `Code.cs`                                                 | Compiled code object             |
| `Vm.cs`                                                   | Virtual machine / interpreter    |
| `Frame.cs`                                                | Call-stack frame                 |
| `Future.cs`, `FutureState.cs`                             | Async promise model              |
| `Cell.cs`                                                 | Closure cell (captured variable) |
| `ObiwanValue.cs`, `ValueType.cs`                          | Runtime value types              |
| `Symbol.cs`, `SymbolTable.cs`                             | Name resolution                  |
| `ScopeType.cs`, `LookupDetail.cs`                         | Scope metadata                   |
| `State.cs`                                                | VM state                         |
| `TryBlock.cs`                                             | Exception-handling frame         |
| `ErrorHandler.cs`                                         | Error dispatch                   |
| `InvalidSwitchValueException.cs`, `ObiwanCompileError.cs` | Error types                      |
| `IBuiltin.cs`                                             | Builtin function interface       |
| `Position.cs`                                             | Source position tracking         |
| `tests/lang.obi`, `tests/import.obi`                      | Example obiwan programs          |
| `test.js`                                                 | Test harness (JavaScript)        |
| `obiwan.csproj`                                           | Project file (net8.0, AOT)       |

---

## Language Syntax Summary

### Comments

```obiwan
// line comment

/* block
   comment */
```

### Variables

```obiwan
var x = 10;
const y = 20;
local z = 30;     // block-scoped
```

### Functions

```obiwan
fn add(a, b) {
    return a + b;
}

// anonymous
var double = fn(x) { return x * 2; };

// async
fn fetch(url) async {
    return await request(url);
}
```

### Control flow

```obiwan
if (cond) { ... } else { ... }

while (g < 10) { g = g + 1; }

switch (val) {
    case 1:
    case 2: { println("1 or 2"); }
    default: println("other");
}
```

### Switch expression

```obiwan
var label = switch (n) {
    0 => "zero",
    1 => "one",
    _ => "other"
};
```

### Arrays and objects

```obiwan
var arr = [1, 2, 3, 4, 5];
var obj = {
    Hello: "World",
    add: fn(a, b) { return a + b; }
};
```

### Destructuring

```obiwan
var [a, b] = someArray();
local { key: alias } = someObject();
```

### Async / Await

```obiwan
fn doWork() async {
    var result = await someAsyncFn();
    return result;
}
```

### Promise chaining

```obiwan
asyncFn()
    ->then(fn(data) { return data + 1; })
    ->then(callback)
    ->error(errorHandler);
```

### Error handling

```obiwan
try {
    await riskyFn();
} catch (err) {
    print "Error:", err;
}
```

---

## License

MIT License — Copyright © 2026 Philipp Andrew Redondo

See [LICENSE](LICENSE) for full text.