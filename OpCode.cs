namespace zscript;

public enum OpCode : byte
{
    LoadLocal,
    LoadCapture,
    LoadConst,
    LoadString,
    LoadNull,
    LoadFunction,
    StoreLocal,
    StoreName,
    Print,
    GetAttr,
    Call,
    CallMethod,
    Await,
    BinMul,
    BinDiv,
    BinMod,
    BinAdd,
    BinSub,
    PopTop,
    SetupTry,
    PopTry,
    Jump,
    Return
}