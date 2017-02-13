## SP Monitoring Tool

### Work in Progress

This is something quick and dirty I whipped up for a customer that I plan on improving and
using for other customers.  There is no testing at all at this point.  All it does is create
a list, and then add new items to that list while timing the request and logging a success or
failure.

### Getting Started

````
git clone https://github.com/mike-morrison/sp-monitor.git new-project-name
````

After that, open the .sln file in Visual Studio and build it in release mode.  Take the output
from the .bin folder and copy everything to a new folder preferably on a server.

There are 2 choices for scheduling.

- Just run the executable from the command line, it should catch all exceptions.
- Schedule it using a Windows Scheduled Task.

**Note:** If you use a scheduled task, the account you run it under needs permissions
to create a list and add items to that SharePoint list.  Also, it is very important that
you select the option to not run the job again if it is already running.  This code doesn't
rely on the scheduling functionality unless the code throws an unhandled exception which
I tried to make sure wasn't possible.  So the schedule is only used to kick off the script
if it crashes, which hopefully it won't.

### Usage

```
SPMonitor <url to site collection> <interval in seconds> <iterations per interval>
```

So if you wanted to run it for a site called "testbed" in http://contoso with an interval of
15 seconds, you would run it like this:

```
SPMonitor http://contoso/sites/testbed 15 5
```

This creates a new list in the testbed site and every 15 minutes it will create 5 new list items
and log the success or failure and duration to a .json log file.  The code uses the SharePoint REST
API and processes the responses using Jil which is a fast JSON parser.

### Limitations

- [x] Rollover logs daily (Untested, but is based on date and should work)
- [ ] Log file cleanup
- [ ] Test data cleanup
- [ ] Ability to time anything other than creating a list item.
- [ ] Other types of authentication (Currently only NTLM/Windows Claims supported)
