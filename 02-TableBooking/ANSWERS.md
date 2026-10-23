# Assignment 02 — Answers

Student name:  Youssef Hany

Group / ID: Monday / Thursday

## What is wrong
- Many Threads bypass this condition [ `if (TablesLeft <= 0)` ] before it is updated 

## How you fixed it
- Added an Lock object in [ `FridayNightBooking.cs` ] class and aplied lock on the the whole `if` condition
