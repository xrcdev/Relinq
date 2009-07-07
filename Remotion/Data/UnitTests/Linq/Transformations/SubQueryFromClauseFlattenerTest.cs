// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// version 3.0 as published by the Free Software Foundation.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Remotion.Collections;
using Remotion.Data.Linq;
using System.Linq;
using Remotion.Data.Linq.Clauses;
using Remotion.Data.Linq.Clauses.Expressions;
using Remotion.Data.Linq.Clauses.ResultModifications;
using Remotion.Data.Linq.Transformations;
using Remotion.Data.UnitTests.Linq.Parsing;

namespace Remotion.Data.UnitTests.Linq.Transformations
{
  [TestFixture]
  public class SubQueryFromClauseFlattenerTest
  {
    private QueryModel _queryModel;
    private MainFromClause _mainFromClause;
    private AdditionalFromClause _additionalFromClause1;
    private AdditionalFromClause _additionalFromClause2;
    private WhereClause _whereClause;
    private SelectClause _selectClause;

    private MainFromClause _innerMainFromClauseA;
    private WhereClause _innerWhereClauseA;
    private OrderByClause _innerOrderByA;

    private SubQueryFromClauseFlattener _visitor;
    private IQueryable<Student_Detail> _detailSource;
    private IQueryable<IndustrialSector> _sectorSource;

    [SetUp]
    public void SetUp ()
    {
      _detailSource = ExpressionHelper.CreateQuerySource_Detail();
      _sectorSource = ExpressionHelper.CreateQuerySource_IndustrialSector();

      var query = from s1 in ExpressionHelper.CreateQuerySource()
                  from sd in
                      (from sector in _sectorSource
                       where sector.ID > 10
                       orderby sector.ID
                       select sector.Student_Detail)
                  from s2 in s1.Friends
                  where sd.Subject == "Maths"
                  select new Tuple<Student, Student_Detail> (s1, sd);
      _queryModel = ExpressionHelper.ParseQuery (query);

      _mainFromClause = _queryModel.MainFromClause;
      _additionalFromClause1 = (AdditionalFromClause) _queryModel.BodyClauses[0];
      _additionalFromClause2 = (AdditionalFromClause) _queryModel.BodyClauses[1];
      _whereClause = (WhereClause) _queryModel.BodyClauses[2];
      _selectClause = (SelectClause) _queryModel.SelectOrGroupClause;

      var subQueryExpressionA = (SubQueryExpression) _additionalFromClause1.FromExpression;
      _innerMainFromClauseA = subQueryExpressionA.QueryModel.MainFromClause;
      _innerWhereClauseA = (WhereClause) subQueryExpressionA.QueryModel.BodyClauses[0];
      _innerOrderByA = (OrderByClause) subQueryExpressionA.QueryModel.BodyClauses[1];

      _visitor = new SubQueryFromClauseFlattener();
    }

    [Test]
    public void VisitAdditionalFromClause_IgnoresNonSubQueries ()
    {
      _visitor.VisitAdditionalFromClause (_additionalFromClause2, _queryModel, 1);

      Assert.That (_queryModel.BodyClauses[1], Is.SameAs (_additionalFromClause2));
      var expectedExpression = ExpressionHelper.Resolve<Student, IEnumerable<Student>> (_mainFromClause, s => s.Friends);
      ExpressionTreeComparer.CheckAreEqualTrees (expectedExpression, _additionalFromClause2.FromExpression);
    }

    [Test]
    public void VisitAdditionalFromClause_ReplacesFromDataWithInnerMainFromData ()
    {
      _visitor.VisitAdditionalFromClause (_additionalFromClause1, _queryModel, 0);

      Assert.That (_queryModel.BodyClauses[0], Is.SameAs (_additionalFromClause1));
      Assert.That (_additionalFromClause1.FromExpression, Is.Not.InstanceOfType (typeof (SubQueryExpression)));
      Assert.That (_additionalFromClause1.FromExpression, Is.SameAs (_innerMainFromClauseA.FromExpression));
      Assert.That (_additionalFromClause1.ItemName, Is.EqualTo ("sector"));
      Assert.That (_additionalFromClause1.ItemType, Is.SameAs (typeof (IndustrialSector)));
    }

    [Test]
    public void VisitAdditionalFromClause_PullsOutInnerBodyClauses ()
    {
      _visitor.VisitAdditionalFromClause (_additionalFromClause1, _queryModel, 0);

      Assert.That (_queryModel.BodyClauses[1], Is.SameAs (_innerWhereClauseA));
      Assert.That (_queryModel.BodyClauses[2], Is.SameAs (_innerOrderByA));
      Assert.That (_queryModel.BodyClauses[3], Is.SameAs (_additionalFromClause2));
      Assert.That (_queryModel.BodyClauses[4], Is.SameAs (_whereClause));
    }

    [Test]
    public void VisitAdditionalFromClause_AdaptsReferencesOfInnerBodyClauses ()
    {
      _visitor.VisitAdditionalFromClause (_additionalFromClause1, _queryModel, 0);

      var orderingExpression = (MemberExpression) _innerOrderByA.Orderings[0].Expression;
      var referenceExpression = (QuerySourceReferenceExpression) orderingExpression.Expression;
      Assert.That (referenceExpression.ReferencedClause, Is.SameAs (_additionalFromClause1));
    }

    [Test]
    public void VisitAdditionalFromClause_AdaptsReferencesToFromClause_WithInnerSelector ()
    {
      _visitor.VisitAdditionalFromClause (_additionalFromClause1, _queryModel, 0);

      var expectedPredicate = 
          ExpressionHelper.Resolve<IndustrialSector, bool> (_additionalFromClause1, sector => sector.Student_Detail.Subject == "Maths");
      ExpressionTreeComparer.CheckAreEqualTrees (expectedPredicate, _whereClause.Predicate);

      var expectedSelector = ExpressionHelper.Resolve<Student, IndustrialSector, Tuple<Student, Student_Detail>> (
          _mainFromClause,
          _additionalFromClause1,
          (s1, sector) => new Tuple<Student, Student_Detail> (s1, sector.Student_Detail));
      ExpressionTreeComparer.CheckAreEqualTrees (expectedSelector, _selectClause.Selector);
    }

    [Test]
    [ExpectedException (typeof(NotSupportedException), ExpectedMessage = "The subquery "
        + "'from Student s in TestQueryable<Student>() select 0 => Distinct()' cannot be flattened and pulled out of the from clause because it "
        + "contains result modifications.")]
    public void VisitAdditionalFromClause_ThrowsOnResultModification ()
    {
      var queryModel = ExpressionHelper.CreateQueryModel ();
      ((SelectClause) queryModel.SelectOrGroupClause).ResultModifications.Add (new DistinctResultModification ());
      var clause = new AdditionalFromClause ("x", typeof (Student), new SubQueryExpression (queryModel));
      _visitor.VisitAdditionalFromClause (clause, queryModel, 0);
    }

    [Test]
    public void VisitMainFromClause_AlsoFlattens ()
    {
      var mainFromSubQuery = from sd in _detailSource
                             where sd.Subject == "Maths"
                             orderby sd.ID
                             select sd.Student;
      var parsedMainFromSubQuery = ExpressionHelper.ParseQuery (mainFromSubQuery);

      var query = from s in ExpressionHelper.CreateQuerySource()
                  select s.First;
      var parsedQuery = ExpressionHelper.ParseQuery (query);
      parsedQuery.MainFromClause.FromExpression = new SubQueryExpression (parsedMainFromSubQuery);

      parsedQuery.Accept (_visitor);

      var expectedSelector = ExpressionHelper.Resolve<Student_Detail, string> (parsedQuery.MainFromClause, sd => sd.Student.First);

      Assert.That (parsedQuery.MainFromClause.FromExpression, Is.Not.InstanceOfType (typeof (SubQueryExpression)));
      Assert.That (parsedQuery.BodyClauses.Count, Is.EqualTo (2));
      ExpressionTreeComparer.CheckAreEqualTrees (expectedSelector, ((SelectClause) parsedQuery.SelectOrGroupClause).Selector);
    }

    [Test]
    [Ignore ("TODO 1308")]
    public void IntegrationTest_TransformedQueryModel ()
    {
      var query = from s1 in ExpressionHelper.CreateQuerySource ()
                  from sd in
                    (from sector in _sectorSource
                     where sector.ID > 10
                     orderby sector.ID
                     select sector.Student_Detail)
                  from s2 in s1.Friends
                  where sd.Subject == "Maths"
                  from s3 in
                    (from a in s1.Friends
                     from b in sd.Student.Friends
                     select new Tuple<Student, Student> (a, b))
                  select new Tuple<Student, Student_Detail, Student, Student> (s1, sd, s3.A, s3.B);

      var queryModel = ExpressionHelper.ParseQuery (query);
      var mainFromSubQuery = from sd in _detailSource
                             where sd.Subject == "Maths"
                             orderby sd.ID
                             select sd.Student;
      var parsedMainFromSubQuery = ExpressionHelper.ParseQuery (mainFromSubQuery);
      queryModel.MainFromClause.FromExpression = new SubQueryExpression (parsedMainFromSubQuery); // change to be a from clause with a sub query

      queryModel.Accept (_visitor);

      var expectedQuery = from sd in _detailSource
                          where sd.Subject == "Maths"
                          orderby sd.ID
                          from sector in _sectorSource
                          where sector.ID > 10
                          orderby sector.ID
                          from s2 in sd.Student.Friends
                          where sector.Student_Detail.Subject == "Maths"
                          from a in sd.Student.Friends
                          from b in sector.Student_Detail.Student.Friends
                          select new Tuple<Student, Student_Detail, Student, Student> (
                              sd.Student, 
                              sector.Student_Detail, 
                              new Tuple<Student, Student> (a, b).A, 
                              new Tuple<Student, Student> (a, b).B);

      var expectedQueryModel = ExpressionHelper.ParseQuery (expectedQuery);
      QueryModelComparer.CheckAreEqualModels (expectedQueryModel, queryModel);
    }
  }
}