# Assignment 04 — Answers

Student name:  Youssef Hany Zakaria
Group / ID: Monday / Thursday

## Why Expected and Actual disagree

- Because the operation `Views++` is done on three stages -> `int temp = Views` -> `temp++` -> `Views = temp`  , so many threads read invalid data of the innner-operations not the whole

## What you changed

- Used to `Interlock.Increment()` to mark the operatio `Views++` as one atomic operarion [indivisible]

## When would you protect the update differently

- If the code was multiple lines and conditions i would have used Lock on the whole block
