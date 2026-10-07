' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Recommendations
    Public Module RecommendationTestHelpers
        Private s_parts As IEnumerable(Of AbstractKeywordRecommender)

        Private Function GetRecommendedKeywords(source As String, position As Integer, parseOptions As VisualBasicParseOptions) As IEnumerable(Of RecommendedKeyword)
            If s_parts Is Nothing Then
                s_parts = GetType(AbstractKeywordRecommender).Assembly.
                    GetTypes().
                    Where(Function(t) t.IsSubclassOf(GetType(AbstractKeywordRecommender))).
                    Select(Function(t) Activator.CreateInstance(t)).
                    Cast(Of AbstractKeywordRecommender)().
                    ToList()
            End If

            Using workspace = New TestWorkspace(composition:=FeaturesTestCompositions.Features)
                Dim solution = workspace.CurrentSolution
                Dim project = solution.AddProject("test", "test", LanguageNames.VisualBasic)
                Dim document = project.AddDocument("test.cs", source)

                Dim tree = DirectCast(SyntaxFactory.ParseSyntaxTree(SourceText.From(source), parseOptions), VisualBasicSyntaxTree)
                Dim comp = VisualBasicCompilation.Create("test", syntaxTrees:={tree}, references:={NetFramework.mscorlib})
                Dim semanticModel = comp.GetSemanticModel(tree)

                Dim context = VisualBasicSyntaxContext.CreateContext(document, semanticModel, position, CancellationToken.None)
                Return s_parts.SelectMany(Function(part) part.RecommendKeywords_Test(context))
            End Using

        End Function

        Private Function GetRecommendedKeywordStrings(source As String, position As Integer, parseOptions As VisualBasicParseOptions) As IEnumerable(Of String)
            Dim keywords = GetRecommendedKeywords(source, position, parseOptions)
            Return keywords.Select(Function(k) k.Keyword)
        End Function

        Friend Sub VerifyRecommendationsAreExactly(testSource As XElement, ParamArray recommendations As String())
            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)
            Dim recommendedKeywords = (GetRecommendedKeywordStrings(source.Replace("|", ""), source.IndexOf("|"c), parseOptions)) _
                                      .OrderBy(Function(recommendation) recommendation) _
                                      .ToArray()

            Assert.Equal(recommendations.OrderBy(Function(recommendation) recommendation).ToArray(), recommendedKeywords)
        End Sub

        Friend Sub VerifyRecommendationDescriptionTextIs(testSource As XElement, keyword As String, text As String)
            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)
            Dim recommendedKeyword = (GetRecommendedKeywords(source.Replace("|", ""), source.IndexOf("|"c), parseOptions)) _
                                     .Single(Function(r) r.Keyword = keyword)
            Dim expectedText = text.Trim()
            Assert.Equal(expectedText, recommendedKeyword.DescriptionFactory(CancellationToken.None).GetFullText())
        End Sub

        Friend Sub VerifyRecommendationsWithPriority(testSource As XElement, priority As Integer, ParamArray recommendations As String())
            Assert.NotEmpty(recommendations)

            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)

            VerifyRecommendationsContainNothingTyped(source, parseOptions, priority, recommendations)
            VerifyRecommendationsContainPartiallyTyped(source, parseOptions, priority, recommendations)
        End Sub

        Friend Sub VerifyRecommendationsContain(testSource As XElement, ParamArray recommendations As String())
            Assert.NotEmpty(recommendations)

            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)

            VerifyRecommendationsContainNothingTyped(source, parseOptions, Nothing, recommendations)
            VerifyRecommendationsContainPartiallyTyped(source, parseOptions, Nothing, recommendations)
        End Sub

        Private Sub VerifyRecommendationsContainNothingTyped(source As String, parseOptions As VisualBasicParseOptions, priority As Integer?, ParamArray recommendations As String())
            ' Test with the | removed
            Dim recommendedKeywords = GetRecommendedKeywords(source.Replace("|", ""), source.IndexOf("|"c), parseOptions)

            Dim recommendedKeywordStrings = recommendedKeywords.Select(Function(k) k.Keyword)
            For Each recommendation In recommendations
                Assert.Contains(recommendation, recommendedKeywordStrings)
            Next

            If priority.HasValue Then
                Assert.All(recommendedKeywords.Where(Function(k) recommendations.Contains(k.Keyword)), Sub(k) Assert.Equal(k.MatchPriority, priority.Value))
            End If

            VerifyRecommendationsHaveDescriptionText(recommendedKeywords.Where(Function(k) recommendations.Contains(k.Keyword)))
        End Sub

        Private Sub VerifyRecommendationsHaveDescriptionText(recommendations As IEnumerable(Of RecommendedKeyword))
            For Each keyword In recommendations
                Assert.NotEmpty(keyword.DescriptionFactory(CancellationToken.None).GetFullText())
            Next
        End Sub

        Private Sub VerifyRecommendationsContainPartiallyTyped(source As String, parseOptions As VisualBasicParseOptions, priority As Integer?, ParamArray recommendations As String())
            ' Test with the | replaced with the first character of the keywords we expect
            For Each partiallyTypedRecommendation In recommendations.Select(Function(recommendation) recommendation(0)).Distinct()
                Dim recommendedKeywords = (GetRecommendedKeywords(source.Replace("|"c, partiallyTypedRecommendation), source.IndexOf("|"c) + 1, parseOptions)).ToArray()
                Dim recommendedKeywordStrings = recommendedKeywords.Select(Function(k) k.Keyword)

                For Each recommendation In recommendations
                    Assert.Contains(recommendation, recommendedKeywordStrings)
                Next

                If priority.HasValue Then
                    Assert.All(recommendedKeywords.Where(Function(k) recommendations.Contains(k.Keyword)), Sub(k) Assert.Equal(k.MatchPriority, priority.Value))
                End If

                VerifyRecommendationsHaveDescriptionText(recommendedKeywords.Where(Function(k) recommendations.Contains(k.Keyword)))
            Next
        End Sub

        Friend Sub VerifyRecommendationsMissing(testSource As XElement, ParamArray recommendations As String())
            Assert.NotEmpty(recommendations)

            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)

            Dim recommendedKeywords = (GetRecommendedKeywordStrings(source.Replace("|", ""), source.IndexOf("|"c), parseOptions)) _
                                      .OrderBy(Function(recommendation) recommendation) _
                                      .ToArray()

            For Each recommendation In recommendations
                Assert.DoesNotContain(recommendation, recommendedKeywords)
            Next
        End Sub

        Friend Sub VerifyNoRecommendations(testSource As XElement)
            Dim source = ConvertTestSourceTag(testSource)
            Dim parseOptions = GetParseOptions(testSource)

            Dim recommendedKeywords = (GetRecommendedKeywordStrings(source.Replace("|", ""), source.IndexOf("|"c), parseOptions)) _
                                      .OrderBy(Function(recommendation) recommendation) _
                                      .ToArray()

            Assert.Empty(recommendedKeywords)
        End Sub

        Private Function GetParseOptions(testSource As XElement) As VisualBasicParseOptions
            Dim parseOptions = New VisualBasicParseOptions().WithKind(If(testSource.@Script = "True", SourceCodeKind.Script, SourceCodeKind.Regular))
            If testSource.@VbNext = "True" Then
                parseOptions = parseOptions.WithLanguageVersion(LanguageVersion.VbNext)
            End If

            Return parseOptions
        End Function
    End Module
End Namespace
