


async function willThrow() {
    throw new Error("Foocer!");
    console.log("HEHE!");
}

async function wrapper() {
    await willThrow();
    console.log("continue wrapper!");
}

async function caller() {
    try {
        await wrapper();
        console.log("continue try!");
    } catch(err) {
        console.log(err);
    }
    console.log("Continue!!!");
}


caller();
console.log("Done!");
