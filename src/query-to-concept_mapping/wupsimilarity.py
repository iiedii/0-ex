'''
Calculate the similarity by WUP distance given two terms
    - One is from event description, the other is from concept names

Created on Jun 26, 2014

@author: Yi-Jie Lu
'''

import math
import time
import sys
from nltk.corpus import wordnet

# Input
InputFile = r'..\..\outputs\AVS16\Event_Concept_TermTable.txt'
# Output
OutputFile = r'..\..\outputs\AVS16\Event_Concept_SimilarityTable.txt'


def Main(argv):
    print 'Processing...'
    
    startTime = time.clock()
    CalculateSimilarity(InputFile, OutputFile)
    #TestSimilarity('cup', 'cup')
    endTime = time.clock()
    
    print '\nTime spent: ' + str(endTime - startTime) + 's'
    print 'Job done.'
    return 0



def CalculateSimilarity(InputFile, OutputFile):
    fileOutput = open(OutputFile, 'w')
    lineID = 1
    for line in open(InputFile):
        if lineID % 10000 == 0:
            print '  - at record', lineID
        
        splitLine = line.rstrip().split('\t')
        word1 = splitLine[0]
        word2 = splitLine[1]
        similarity = CalcWUPSimilarity_Default(word1, word2)
        fileOutput.write('%s\t%s\t%s\n' % (word1, word2, similarity))
        lineID += 1
    return



def CalcWUPSimilarity_Default(word1, word2):
    try:
        synset1 = wordnet.synsets(word1)[0]         #! probably inaccurate mapping -- guess [0] is a semantic match; cost time 223   @UndefinedVariable
        synset2 = wordnet.synsets(word2)[0]         # synset2 is from concept name; cost time 178, tried with Dict, no help   @UndefinedVariable
                    
        if isMatch(synset1, synset2):
            minDepth = synset1.min_depth()              # synset1 is from event description, word specificity; cost time 509, move here to save time
            if minDepth == 0:
                if '.n.' not in synset1.name():
                    minDepth = math.exp(1)
                else:
                    print '   - hit minDepth=0, synset=' + synset1.name
                    return 0
                
            return 1 * math.log(minDepth)           # combine with word specificity
            #return 1
            #return 1000 * math.log(minDepth)
        
        return 0
#         similarity = synset1.wup_similarity(synset2)            # cost time 5088
#         if similarity == None:
#             return 0
#         else:
#             return 0                                # for exact match, WUP similarity is not used
#             #return similarity * math.log(minDepth)            # WUP similarity combined with word specificity
#             #return (10000.0 ** similarity - 1.0) / 9999.0 # * math.log(minDepth)
    except IndexError:
        if word1 == word2:                          # avoid if the word is not found in WordNet
            return 1
        else:
            return -1                               # word1 or word2 is not found
        #raise IndexError(word1 + ', ' + word2)



def isMatch(synset1, synset2):
    # Judge if exactly matches 
    if synset1 == synset2:                          # exact match
        return True
        
#     # Judge if a child matches
#     if '.n.' not in synset1.name():                 # the word from event description is not a noun
#         return False
#     if isChildNode(synset1, synset2):               # when the word from event description is a noun, further check if synset2 is a child node
#         return True
    
    return False


def isChildNode(synset1, synset2):
    # Determine whether synset2 is a child node of synset1
    childSet1L1 = set(synset1.hyponyms())           # 1st-layer children
    childSet1L2 = set()                             # 2nd-layer children
#     for childSynset in childSet1L1:
#         childSet1L2 = childSet1L2 | set(childSynset.hyponyms())
    
    if synset2 in childSet1L1 | childSet1L2:
        return True
    else:
        return False



def TestSimilarity(word1, word2):
    similarity = CalcWUPSimilarity_Default(word1, word2)
    print similarity
    return


if __name__ == '__main__':
    Main(sys.argv[1:])
    