using Tests;

var t = new LoadBalancerDemoScenarios();
await t.BasicScenario(globalRequestsPerSecond: 1000);