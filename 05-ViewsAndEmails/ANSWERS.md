# Assignment 05 — Answers

Student name:  Youssef Hany
Group / ID: Monday / Thursday

## Failures you observed
- The normal Dictionary caused an error on this line `Views.Add(product, 0);` Many threads bypass the condition together, we try to add element already there
- `EmailJobs.Enqueue($"Email: order {orderId}");` some insertion are lost because adding an element takes some steps in queue not an atomic operation

## What you changed

- Changed `Dictionary` to `ConcurrentDictionary`
- Chnaged the Queue to `ConcurrentQueue` 

## Collection safety vs business-rule safety

- Collection safety is protecting the collection operations from multi-threads access at same time
- Business safety is protecting a block if operations to execute together

