# Assignment 03 — Answers

Student name:  Youssef Hany
Group / ID: Monday / Thursday

1. ` System.Threading.Monitor` provide extra functions to implement a specific `lock` scenario , allowing one thread to enter at a time

2. ```
    Monitor.Enter(locker);
    try
    {
        if(count>0)
        count--;
    }
    finally
    {
        Monitor.Exit(locker);
    }
    

3. - If the the requirements is simple like example at `point 2` -> No difference, while using lock still claener in code and prefered
    - `Monitor` Provides more functions and allow implementation of complex scenarios

4. - `Monitor.TryEnter` if i want to lock on object for a specific time  like `Monitor.TryEnter(_sync,1000)` or if a bool is true `Monitor.TryEnter(_sync,isValid)`
    - `Monitor.Wait` -> Release the lock, block this thread and enter waiting state. USE WHEN i want to wait this thread only
    - `Monitor.Pulse` -> Release the lock and the runtime(not ThreadPool) chooses one random Thread that is waiting now and awake it , USE IT when o want to free `one` waiting thread
    - `Monitor.PulseAll` awake all the waiting threads

5. ```
    Monitor.Enter(locker);
    try
    {
        if(count>0)
            count--;
    }
    finally
    {
        Monitor.Exit(locker);
    }
    ```

    ```
    lock(locker){
        if(count>0)
        count--;
    }

Links:

- https://chatgpt.com/share/6aca87dc-3610-83ea-ad44-9d4e3f1bd40d
