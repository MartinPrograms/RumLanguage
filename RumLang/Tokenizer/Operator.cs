namespace RumLang.Tokenizer;

public enum Operator
{
    Plus, // +
    Minus, // -
    Asterisk, // *, can be used for multiplication, and creating or dereferencing pointers
    Divide, // /
    Modulus, // %
    Assignment, // =, used for assigning values to variables
    Equal, // ==
    NotEqual, // !=
    GreaterThan, // >
    LessThan, // <
    GreaterThanOrEqual, // >=
    LessThanOrEqual, // <=
    And, // &&, logical AND
    Or, // ||, logical OR
    Not, // !, logical NOT
    Increment, // ++, used for incrementing a variable
    Decrement, // --, used for decrementing a variable
    BitwiseAnd, // &, bitwise AND
    BitwiseOr, // |, bitwise OR
    BitwiseXor, // ^, bitwise XOR
    BitwiseNot, // ~, bitwise NOT
    LeftShift, // <<, bitwise left shift
    RightShift, // >>, bitwise right shift
    PointerAccess,
    MemberAccess,
    Variadic, // ..., used for variadic functions
}