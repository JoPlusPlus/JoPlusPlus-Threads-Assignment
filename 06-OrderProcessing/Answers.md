# Phase 1 Questions
## 1. Why are the sync and async times almost the same when orders are awaited one after another?
Each await finishes before the next order starts so the work is still one order at a time. Async only frees the thread while waiting so it doesnt loop faster

## 2. Is the thread ID before an await always the same as after it? Why? Use your own log lines as evidence.
no they are not, because the ThreadPool may send another thread to continue the work not always the same thread
`[17:33:09.276] [Thread 12] OrderValidator -> Validating order #8` 
`[17:33:12.330] [Thread 13] OrderRepository -> Order #8 saved (Validated)` 
the validation started at thread 12 and ended by thread 13

## 3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?
while `Task.delay()` it returns to ThreadPool and execute another work, while `Thread.Sleep()` blocks the thread 

## 4. Why should a repository method return Task<T> instead of T?
we return Task so that we can use await on it

## 5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?
`File.ReadAllText` -> thread is blocked while reading
`File.ReadAllTextAsync` -> thread is free to do another work until reading finish

# Phase 2 Questions
## 1. What is the difference between starting a task and awaiting it?
starting two tasks happen at same time while awaiting wait foreach task to finish then start the next one

## 2. Does every Task create a new thread? Does Task.Delay() use a thread while waiting?
- No Threads are controlled by `ThreadPool`.
- No the thread return to `ThreadPool`

## 3. What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous? Write a small separate demo method - the only place .Result / .Wait() is allowed.

- Calling `.Result()` or `.Wait()` will block the thread until the result come while using await free the thread until the Task finish
- They are dangerous because of blocking
- Wrote it in `(Runners/BlockingDemo.cs)`

## 4.What happens to an exception thrown inside a task that is never awaited?
- usually wont crash the app

## 5. When should you use Task.Run(), and when should you avoid it?
- Use `Task.Run` when we want a new Thread doing this work
- avoid it in I/O operation since we dont want Thread at all , just use await

## 6. What is the difference between Task and Task<T>?
- `Task` doesnt return anything but enable the `await` keyword , while `Task<T>` returns T

## 7. Why can a lock block not contain an await?
- because await free the thread to do another work and may return another thread to continue , and in `lock` we want only the thread entered and locked the lock, he only should release it

## 8. Why did the unprotected stock reservation oversell, and how did you prevent it?
- it oversells because both reads the same data at same time
- i prevented it by using `_store.UpdateAsync` which lock the file until it reads,update . now when the other thread come to read , it will read an updated data

# Phase 3 Questions
## Seciton 9.6
1.  Does async make one order's workflow faster? 
No

2. Why does async scale better for many orders?
because we will avoid making threads by orders numbers , and reuse the threads while performing I/O

3. What happens to the sync version with 1,000 orders?
it will need nearly 1000 threads 

4. Why is async essential for apps that call databases, files and APIs?
reuse the same thread while performing I/O

## Seciton 9.7
1. What is the problem in this code?
we didn't use await for `SendConfirmationAsync`, 

2. Show what happens if the email is still being sent when the method returns, or when the console app exits.
the app might exit before the email is sent and in that case it lost forever

3. Show what happens if the email operation throws an exception.
won't reach to the caller

4. 
Before: the method returned with 0 records in `emails.json` and the exception was lost.
After:
```
Order processing finished
emails.json right after the method returned: 1 record(s)
```
and when the email fails:
```
FireAndForgetDemo -> The caller can see the email error: SMTP server unavailable (attempt 2)
Order processing finished
```

5. When is await necessary?
when i do I/O alot and need to reuse the threads 

6. When is fire-and-forget acceptable (if ever)?
if the work done is optional and it's ok to be lost when app exit

7. Why is `async void` dangerous?
the caller can't use await on it , and won't know the status. with `async Task` we can do both

# Phase 4 Questions

## 1. Which tool did I use for each task, and why can't "start, then await one by one" meet the requirement?

starting one by one will take much time

## 2. In 10.2, when several loads fail, how do I get every exception? What does a plain `await` give me?

A plain `await` on `Task.WhenAll(...)` throws only the first exception even if more loads failed. to get all of them I keep the task in a variable (`Task all = Task.WhenAll(...)`) and after `await all` fails I read `all.Exception.InnerExceptions`. then In my code I throw `all.Exception` and prints each inner exception.

## 3. In 10.3, what happens to the losing carriers if I never cancel them?

They keep running until they finish by themselves, even if nobody needs their answer anymore.

## 4. Why does 10.4 A get faster with more CPU cores, while 10.4 B does not?

because 10.4A is CPU-Bound , so adding more cores will make the calculations faster
while 10.4B is I/O operations so adding more cores is useless

## 5. What is the difference between parallel work (10.4 A) and concurrent asynchronous work (10.1)?

Parallel means all Threads execute in the same time in parallel
which concurrent asynchronous , they execute one after another

## 6. Cancelled run


## 10.4 B: why is the total time about 8 seconds?

Each send takes 200 + 300 + 500 = 1000 ms. With 4 sends at the same time, 30 emails need 30 ÷ 4 = 7.5 rounds, so 8 rounds of about 1 s, which is about 8 s. My run took 8.1 s.