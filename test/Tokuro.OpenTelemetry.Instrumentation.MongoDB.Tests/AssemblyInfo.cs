// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

// All tests observe one process-global ActivitySource ("Tokuro..."). Running collections in
// parallel lets one class's listener (e.g. the OTel TracerProvider in
// TracerProviderBuilderExtensionsTests, which samples AllData) change the sampling decision
// seen by another class's activities. Serialize the suite to keep each test deterministic.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
