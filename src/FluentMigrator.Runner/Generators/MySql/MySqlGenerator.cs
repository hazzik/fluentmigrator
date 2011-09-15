#region License
// 
// Copyright (c) 2007-2009, Sean Chambers <schambers80@gmail.com>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace FluentMigrator.Runner.Generators.MySql
{
	using System;
	using Expressions;
    using Generic;

    public class MySqlGenerator : GenericGenerator
	{
		public MySqlGenerator() : base(new MySqlColumn(), new MySqlQuoter())
		{
		}

        public override string AlterColumn { get { return "ALTER TABLE {0} MODIFY COLUMN {1}"; } }

        public override string DeleteConstraint { get { return "ALTER TABLE {0} DROP FOREIGN KEY {1}"; } }

        public override string CreateTable { get { return "CREATE TABLE {0} ({1}) ENGINE = INNODB"; } }

        public override string Generate(DeleteIndexExpression expression)
        {
            return string.Format("DROP INDEX {0} ON {1}", Quoter.QuoteIndexName(expression.Index.Name), Quoter.QuoteTableName(expression.Index.TableName));
        }

		public override string Generate(RenameColumnExpression expression)
		{
			return string.Format(@"
SELECT CONCAT(
          'ALTER TABLE `{0}` CHANGE `{1}` `{2}` ',
          CAST(COLUMN_TYPE AS CHAR),
          IF(ISNULL(CHARACTER_SET_NAME),
             '',
             CONCAT(' CHARACTER SET ', CHARACTER_SET_NAME)),
          IF(ISNULL(COLLATION_NAME),
             '',
             CONCAT(' COLLATE ', COLLATION_NAME)),
          ' ',
          IF(IS_NULLABLE = 'NO', 'NOT NULL ', ''),
          IF(IS_NULLABLE = 'NO' AND COLUMN_DEFAULT IS NULL,
             '',
             CONCAT('DEFAULT ', QUOTE(COLUMN_DEFAULT), ' ')),
          UPPER(extra))
 INTO @change_statement
  FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME = '{0}' AND COLUMN_NAME = '{1}';

PREPARE r FROM @change_statement;
EXECUTE r;
DEALLOCATE PREPARE r;", Quoter.QuoteCommand(expression.TableName), Quoter.QuoteCommand(expression.OldName), Quoter.QuoteCommand(expression.NewName));
		}

    	public override string Generate(AlterDefaultConstraintExpression expression)
		{
            return compatabilityMode.HandleCompatabilty("Altering of default constrints is not supporteed for MySql");
		}
	}
}
