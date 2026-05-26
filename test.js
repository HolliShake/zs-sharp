


async function willThrow() {
    throw new Error("Fooc!");
}

async function caller() {
    try {
        await willThrow();
    } catch(err) {
        console.log(err);
    }
    console.log("Continue!!!");
}


caller();
console.log("Done!");
