﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

namespace FluentAssertions.BestPractices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CollectionShouldEqualOtherCollectionByComparerAnalyzer : FluentAssertionsAnalyzer
    {
        public const string DiagnosticId = Constants.Tips.Collections.CollectionShouldEqualOtherCollectionByComparer;
        public const string Category = Constants.Tips.Category;

        public const string Message = "Use {0} .Should() followed by .Equal() instead.";

        protected override DiagnosticDescriptor Rule => new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, DiagnosticSeverity.Info, true);

        protected override IEnumerable<(FluentAssertionsCSharpSyntaxVisitor, BecauseArgumentsSyntaxVisitor)> Visitors
        {
            get
            {
                yield return (new SelectShouldEqualOtherCollectionSelectSyntaxVisitor(), new BecauseArgumentsSyntaxVisitor("Equal", 1));
            }
        }

        private class SelectShouldEqualOtherCollectionSelectSyntaxVisitor : FluentAssertionsWithArgumentsCSharpSyntaxVisitor
        {
            private ExpressionSyntax _lambdaArgument;
            private string _otherVariable;

            protected override bool AreArgumentsValid()
            {
                if (Arguments.TryGetValue(("Select", 0), out var selectArgument) && selectArgument is SimpleLambdaExpressionSyntax select
                    && Arguments.TryGetValue(("Equal", 0), out var expectedArgument) && expectedArgument is InvocationExpressionSyntax expected)
                {
                    var visitor = new SelectSyntaxVisitor();
                    expected.Accept(visitor);

                    if (visitor.IsValid)
                    {
                        _otherVariable = visitor.VariableName;
                        _lambdaArgument = SyntaxFactory.ParenthesizedLambdaExpression(
                            parameterList: SyntaxFactory.ParameterList().AddParameters(select.Parameter, visitor.Lambda.Parameter),
                            body: SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,
                                left: (ExpressionSyntax)select.Body,
                                right: (ExpressionSyntax)visitor.Lambda.Body)
                        ).NormalizeWhitespace();
                        return true;
                    }
                }
                return false;
            }

            public SelectShouldEqualOtherCollectionSelectSyntaxVisitor() : base("Select", "Should", "Equal")
            {
            }

            public override ImmutableDictionary<string, string> ToDiagnosticProperties() => base.ToDiagnosticProperties()
                .Add(Constants.DiagnosticProperties.LambdaString, _lambdaArgument.ToFullString())
                .Add(Constants.DiagnosticProperties.ArgumentString, _otherVariable);


            private class SelectSyntaxVisitor : FluentAssertionsWithLambdaArgumentCSharpSyntaxVisitor
            {
                protected override string MethodContainingLambda => "Select";
                public SelectSyntaxVisitor() : base("Select")
                {
                }
            }
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionShouldEqualOtherCollectionByComparerCodeFix)), Shared]
    public class CollectionShouldEqualOtherCollectionByComparerCodeFix : FluentAssertionsCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CollectionShouldEqualOtherCollectionByComparerAnalyzer.DiagnosticId);

        protected override StatementSyntax GetNewStatement(FluentAssertionsDiagnosticProperties properties)
            => SyntaxFactory.ParseStatement($"{properties.VariableName}.Should().Equal({properties.ArgumentString}, {properties.CombineWithBecauseArgumentsString(properties.LambdaString)});");
    }
}
