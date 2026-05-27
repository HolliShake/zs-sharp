async function println(a) {
    console.log(a);
}

async function willThrow() {
    // ⚠️  In the original language 2 + "Fooc!" is a type error.
    //     In JS it silently coerces to "2Fooc!" — throwing explicitly to match intent.
    throw new TypeError('Unsupported operand types: number + string ("Fooc!")');
    println("HEHE!");         // unreachable — same as original
}

async function wrapper() {
    await willThrow();
    println("continue wrapper!");
}

async function caller() {
    try {
        await wrapper();
        println("continue try!");
    } catch (err) {
        console.log("FROM ERROR!!", err);
    }
    println("Continue!!!");
}

// ⚠️  No await here — caller() runs concurrently with println below,
//     same fire-and-forget semantics as the original.
caller();
println("Done!");   // prints before caller()'s internals resolve


function cb(data) {
    console.log("CB:", data);
    return data + 1;
}

function cbForError(data) {
    console.log("cb error", data);
}

function cbError(data) {
    console.log("CB ERROR", data);
}

async function add(a, b) {
    // ⚠️  add(1, "str") won't reject in JS — 1 + "str" = "1str".
    //     Guard added to match original type-strict behaviour.
    if (typeof a !== typeof b) {
        throw new TypeError(`Unsupported operand types: ${typeof a} + ${typeof b}`);
    }
    return a + b;
}

// ->then()  maps directly to .then()
// ->catch() maps directly to .catch()
add(0, 1)
    .then(cb)
    .then(cb)
    .then(cb);


add(0, "foccers")
    .catch(cbError);