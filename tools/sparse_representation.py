'''
This code changes a comma-seperated dense feature format to a sparse format.
Output format: {dimension_index}:{value},{dimension_index}:{value}...
Note that dimension_index starts from 0.

@author: Yi-Jie Lu
'''


# Input
sparse_score_table_file = r'..\datasets\AVS16\IACC.3_SRIP2K_DRN_Features\score_table_sparse.csv'
# Output
output_file = r'..\datasets\AVS16\IACC.3_SRIP2K_DRN_Features\score_table_sparse_repr.csv'


print 'Reading score table...'
with open(sparse_score_table_file) as fin:
	with open(output_file, 'w') as fout:
		probeDimension = None
		lineID = 0
		for line in fin:
			lineID += 1
			splitLine = line.rstrip().split(',')
			if probeDimension is None:
				probeDimension = len(splitLine)
				print '   - dimension =', probeDimension
			else:
				assert len(splitLine) == probeDimension

			lineParts = []
			for index, value in enumerate(splitLine):
				if float(value) != 0.0:
					lineParts.append('%s:%s' % (index, value))

			if len(lineParts):
				fout.write(','.join(lineParts) + '\n')
			else:
				fout.write('0:0\n')

			if lineID % 10000 == 1:
				print '   - at Line', lineID

print '\nJob done.'
raw_input()
