# Assignment 01 — Answers

Student name:  YOUSSEF HANY
Group / ID: Monday/Thursday

## Problems with the current design
- export.ExportAllPending() -> calls a chain of synchronous functions , each uses Thread.Sleep(), causing 1s for each order process + 0.8s for fetching all orders
    - GetPendingOrders() -> 800ms once
    - GetOrderById() -> 400ms
    - ReadProfile() -> 300ms per order
    - WriteInvoice() -> 300ms

- GetOrderById() is useless because we already have all orders why still get each one by one?
- File.ReadAllText and File.WriteAllText are blocking we can convert them to ..Async and use wait, to avoid blocking the thread while performing I/O Operations 
- Updated the for loop -> separated the logic in another function , to allow starting all order processing at one time and waiting once for all, instead of waiting for each order to finish then process the next one 
- Actually last point i USED AI  when i noticed the TIME is still 3.9s , and it told me the solution and i understood it 

## What you changed
- Swtiched All functions that uses  Thread.Sleep() -> await Task.Delay()
- Removed GetOrderById method cause it useless
- Replaced Every File.ReadAllText and File.WriteAllText with the Async version
- Now The time is 1.4s
