# zscript

A lightweight scripting language and interpreter implemented in C# (.NET 8).
Designed for embedding, scripting, and experimenting with language features.

## Features

- Custom scripting language with async/await-like syntax
- Arithmetic, function calls, and error handling
- Test harness and CLI for running scripts
- Example language file (`lang.txt`) included

## Example

```zscript
fn println(a) async {
    print ">>", a;
}

fn add(a, b) async {
    print "waiting...", await println(a + b + 10);
    await println("Hola!");
    return 1;
}

add(323, 413)
    ->then(callback)
    ->then(callback)
    ->then(callback);
```

## Usage

Build and run with .NET 8:

```sh
dotnet build
dotnet run -- -r path/to/script.zs
```

### CLI Options

- `-r, --run <path>`: Run a zscript source file
- `-t, --test`: Run the internal test suite
- `-h, --help`: Show help

## Build & Publish

To build and publish a self-contained binary:

```sh
dotnet publish -c Release
```

## Project Structure

- `Program.cs`: CLI entry point
- `Vm.cs`: Virtual machine/interpreter
- `Compiler.cs`, `Parser.cs`, `Lexer.cs`: Language frontend
- `lang.txt`: Example script

## License

MIT License  
Copyright (c) 2026 Philipp Andrew Redondo
