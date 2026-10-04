# VB-054 TypeOf over several types

This note locks the spelling for slice 12a. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

```vb
If TypeOf value Is {Customer, Order} Then
End If

If TypeOf value IsNot {Customer, Order} Then
End If
```

Braces hold the type list. `Is` matches when the value is any listed type. `IsNot` matches when it is none of them. The value is evaluated once. Parentheses stay out of this form because they already start a tuple. A single `TypeOf value Is Customer` is unchanged.
