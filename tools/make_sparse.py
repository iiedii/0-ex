'''
This code makes a score table sparse.

@author: Yi-Jie Lu
'''

import numpy as np

# Input
score_table_file = r'..\datasets\AVS16\IACC.3_SRIP2K_DRN_Features\score_table.csv'
# Parameter
zero_thresh = 5e-2        # 5e-2 is good to start for softmaxed CNN features with thousands of dimensions
# Output
output_file = r'..\datasets\AVS16\IACC.3_SRIP2K_DRN_Features\score_table_sparse.csv'


print 'Reading score table...'
with open(score_table_file) as fin:
	with open(output_file, 'w') as fout:
		probeDimension = None
		lineID = 0
		sumOfZeros = 0
		sumOfNumbers = 0
		for line in fin:
			lineID += 1
			splitLine = line.rstrip().split(',')
			if probeDimension is None:
				probeDimension = len(splitLine)
				print '   - dimension =', probeDimension
			else:
				assert len(splitLine) == probeDimension

			vector = np.asarray(map(float, splitLine))
			zeroValueMask = vector < zero_thresh
			vector[zeroValueMask] = 0

			# Calculate percentage of zeros
			sumOfZeros += np.sum(zeroValueMask)
			sumOfNumbers += probeDimension

			vectorStr = map(str, vector)
			fout.write(','.join(vectorStr) + '\n')

			if lineID % 10000 == 1:
				print '   - at Line', lineID
				print '   - percentage of zeros = %.1f%%' % (float(sumOfZeros) / sumOfNumbers * 100.0)



print '\nJob done.'
raw_input()
