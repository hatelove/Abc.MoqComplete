﻿using System.Linq;
using Abc.MoqComplete.Services;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Conversions;
using JetBrains.ReSharper.Psi.CSharp.Impl;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace Abc.MoqComplete.CodeAnalysis
{
    [ElementProblemAnalyzer(typeof(IInvocationExpression), HighlightingTypes = new[] { typeof(SuspiciousCallbackWarning) })]
    public class SuspiciousCallbackAnalyzer : ElementProblemAnalyzer<IInvocationExpression>
    {
        protected override void Run(IInvocationExpression element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var methodIdentifier = element.GetSolution().GetComponent<IMoqMethodIdentifier>();
            var mockedMethodProvider = element.GetSolution().GetComponent<IMockedMethodProvider>();

            if (!methodIdentifier.IsMoqCallbackMethod(element))
                return;

            var expectedtypeParameters = element.TypeArguments;
            if (expectedtypeParameters.Count == 0)
                return;

            var pointer = element.InvokedExpression;
            TreeNodeCollection<ICSharpArgument>? arguments = null;

            while (pointer != null && arguments == null && pointer.FirstChild is IInvocationExpression methodInvocation)
            {
                arguments = mockedMethodProvider.GetMockedMethodParametersFromSetupMethod(methodInvocation);
                pointer = methodInvocation.InvokedExpression;
            }

            if (arguments == null)
                return;

            var actualTypesParameters = arguments.Value.Select(x => x.Value.Type()).ToArray();
            var rule = element.GetPsiModule().GetTypeConversionRule();

            if (actualTypesParameters.Length <= 0)
                return;

            if (expectedtypeParameters.Count != actualTypesParameters.Length)
                AddWarning(element, consumer);
            else
            {
                for (int i = 0; i < expectedtypeParameters.Count; i++)
                {
                    var actualParameterType = actualTypesParameters[i];
                    var expectedParameterType = expectedtypeParameters[i];

                    if (!actualParameterType.Equals(expectedParameterType) && !actualParameterType.IsImplicitlyConvertibleTo(expectedParameterType, rule))
                        AddWarning(element, consumer);
                }
            }
        }

        private static void AddWarning(IInvocationExpression element, IHighlightingConsumer consumer)
        {
            DocumentRange range;
            if (element.FirstChild?.LastChild is ITypeArgumentList typeInvocation)
                range = typeInvocation.GetDocumentRange();
            else
                range = element.InvokedExpression.GetDocumentRange();

            consumer.AddHighlighting(new SuspiciousCallbackWarning(element, range));
        }
    }
}
