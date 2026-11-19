using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Api.Services;

namespace Api.Tests;

/// <summary>Days taken this year counts Approved leave only (Pending and Rejected are not taken).</summary>
public class LeaveSummaryServiceTests
{
    // Employee 1 has no seed leave. Mon-Wed this year = 3 working days.
    static async Task<decimal> DaysTakenWith(LeaveStatus status)
    {
        using var db = TestDb.Create();
        var monday = Seed.MondayOnOrAfter(new DateOnly(DateTime.UtcNow.Year, 6, 1));
        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = 1, StartDate = monday, EndDate = monday.AddDays(2), Status = status });
        await db.SaveChangesAsync();
        return await new LeaveService(db).DaysTakenThisYearAsync(1);
    }

    [Fact]
    public async Task Approved_leave_counts_as_taken() => Assert.Equal(3, await DaysTakenWith(LeaveStatus.Approved));

    [Fact]
    public async Task Pending_leave_is_not_taken() => Assert.Equal(0, await DaysTakenWith(LeaveStatus.Pending));

    [Fact]
    public async Task Rejected_leave_is_not_taken() => Assert.Equal(0, await DaysTakenWith(LeaveStatus.Rejected));

    [Fact]
    public async Task Approved_half_day_counts_half()
    {
        using var db = TestDb.Create();
        Assert.Equal(0.5m, await new LeaveService(db).DaysTakenThisYearAsync(7)); // seed: approved AM half day this year
    }
}

/// <summary>GET /employees/{id}/summary over HTTP: access comes from AccessService, unknown ids are 404.</summary>
public class LeaveSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Employee_sees_their_own_summary()
    {
        var farah = await api.SignedInAs(People.Farah); // employee 6, seed: 3 approved days this year
        var res = await farah.GetAsync("/employees/6/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Summary>();
        Assert.Equal(new Summary(6, 3), body);
    }

    record Summary(int EmployeeId, decimal DaysTakenThisYear);

    [Fact]
    public async Task Another_employee_gets_403()
    {
        var gopal = await api.SignedInAs(People.Gopal); // employee 7, not Farah's manager
        var res = await gopal.GetAsync("/employees/6/summary");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_employee_gets_404()
    {
        var hr = await api.SignedInAs(People.Dina); // HR may see everyone, so the 404 is not hidden by a 403
        var res = await hr.GetAsync("/employees/99999/summary");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
